using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Sungero.Company;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Domain.Shared;
using Sungero.Metadata;
using System.Collections;

namespace DirRX.ClearCPDuplicates.Server
{
  public class ModuleJobs
  {
    /// <summary>
    /// Фоновый процесс изменения поля организации в документах.
    /// </summary>
    public virtual void DocumentsChangeOriginalCompanies()
    {
      #region Конвертации договорных документов.
      Logger.Debug("DocumentsChangeOriginalCompanies. Старт конвертации данных договорных документов.");
      var contractualDocuments = Sungero.Docflow.ContractualDocumentBases.GetAll().Where(x => x.Counterparty != null && ClearCPDuplicatesTemplate.Companies.Is(x.Counterparty)
                                                                                         && ClearCPDuplicatesTemplate.Companies.As(x.Counterparty).DoubleStatusDirRX == ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsDouble
                                                                                         && ClearCPDuplicatesTemplate.Companies.As(x.Counterparty).OriginalCompanyDirRX != null).ToList();
      Logger.DebugFormat("DocumentsChangeOriginalCompanies. Найдено договорных документов {0}", contractualDocuments.Count);
      
      

      
      
      foreach (var doc in contractualDocuments)
      {
        Logger.DebugFormat("DocumentsChangeOriginalCompanies. Обработка документа {0} (ID={1}). Замена организации {2} (ID={3})",
                           doc.Name, doc.Id, doc.Counterparty.Name, doc.Counterparty.Id);
        // Транзакция для обхода наведенной ошибки "Транзакция откатилась" при сохранении всех последующих сущностей.
        Transactions.Execute(
          () =>
          {
            try
            {
              var newCompany = ClearCPDuplicatesTemplate.Companies.As(doc.Counterparty).OriginalCompanyDirRX;
              doc.Counterparty = newCompany;
              doc.Save();
              Logger.DebugFormat("DocumentsChangeOriginalCompanies. Успешное обновление документа {0} (ID={1}). Новая организация {2} (ID={3})",
                                 doc.Name, doc.Id, newCompany.Name, newCompany.Id);
            }
            catch (Exception ex)
            {
              Logger.DebugFormat("DocumentsChangeOriginalCompanies. Невозможно обновить поле Организация для документа {0} (ID={1}). Error: {2} StackTrace: {3}",
                                 doc.Name, doc.Id, ex.Message, ex.StackTrace);
            }
          });
      }
      #endregion
    }

    /// <summary>
    /// Фоновый процесс удаления дублей из справочника Организации.
    /// </summary>
    public virtual void DeleteDoublesCompanies()
    {
      Logger.Debug("DeleteDoublesCompanies. Старт удаления дублей в справочнике Организации.");
      var companies = ClearCPDuplicatesTemplate.Companies.GetAll(c => c.DoubleStatusDirRX == ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsDouble);
      
      if (!companies.Any())
      {
        Logger.Debug("DeleteDoublesCompanies. Дубли организаций - не обнаружены. Конец выполнения фонового процесса.");
        return;
      }
      
      var successfullyCount = 0;
      var companiesCount = companies.Count();

      foreach (var company in companies)
      {
        var id = company.Id;

        try
        {
          // Транзакция для обхода наведенной ошибки "Транзакция откатилась" при сохранении всех последующих сущностей.
          var isDeleted = Transactions.Execute(
            () =>
            {
              ClearCPDuplicatesTemplate.Companies.Delete(company);
              Logger.DebugFormat("DeleteDoublesCompanies. Успешное удаление записи справочника Организации с Id = {0}.", id);
              successfullyCount++;
            });
          
          if (!isDeleted)
            Logger.ErrorFormat("DeleteDoublesCompanies. Невозможно удалить запись справочника Организации: Id = {0}.", id);
        }
        catch (Exception ex)
        {
          Logger.ErrorFormat("DocumentsChangeOriginalCompanies. Ошибка при выполнении транзакции. Error: {0} StackTrace: {1}",
                             ex.Message, ex.StackTrace);
        }
      }

      // Отправка уведомления ответственному если не все дубли были удалены.
      var doubleCompanies = ClearCPDuplicatesTemplate.Companies.GetAll(c => c.DoubleStatusDirRX == ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsDouble);

      if (doubleCompanies.Any())
      {
        var entityList = new List<IEntity>();
        const string subject = "При удалении дублей организаций не все записи были удалены";

        
        foreach (var company in doubleCompanies)
        {
          // Если эта организация уже отправлялась ранее, то исключить из повторной отправки.
          var tasks = Sungero.Workflow.SimpleTasks.GetAll(t => t.Subject == subject).Any(t => t.Attachments.Any(a => a.Id == company.Id));
          if (!tasks)
            entityList.Add(company);
        }

        if (entityList.Count > 0)
        {
          var text = string.Format("При удалении дублей организаций не все записи были удалены{0}Подробности в логе, обратитесь к администратору.", Environment.NewLine);
          Functions.Module.SendNoticeToResponsibleWithAttachments(subject, text, entityList, Sungero.Workflow.SimpleTask.AssignmentType.Notice);
        }
      }

      Logger.DebugFormat("DeleteDoublesCompanies. Завершение удаления дублей справочника Организации. Успешно удалено {0} записей из {1}.",
                         successfullyCount, companiesCount);
    }

    /// <summary>
    /// Фоновый процесс определения дублей и подозрений на дубли среди записей справочника Организации.
    /// </summary>
    public virtual void CompaniesCheckDoubles()
    {
      Logger.Debug("CompaniesCheckDoubles. Старт обработки дублей в справочнике Организации.");

      #region Обработка организаций с настройками обмена.
      Logger.Debug("CompaniesCheckDoubles. Обработка организаций с настройками обмена.");

      var companies = ClearCPDuplicatesTemplate.Companies.GetAll()
        .Where(c => c.OriginalCompanyDirRX == null).ToList()
        .Where(c => (c.ExchangeBoxes.Any() || c.CanExchange == true) && !c.DoubleStatusDirRX.HasValue);
      Logger.DebugFormat("CompaniesCheckDoubles. Найдено организаций с настройками обмена {0}.", companies.Count());

      foreach (var company in companies)
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка организации {0} (ID={1}) с настройками обмена.", company.Name, company.Id);
        var doubles = Functions.Module.FindAllOrgDoubles(company, false);
        if (doubles?.Count > 0)
        {
          foreach (var orgDouble in doubles)
          {
            Functions.Module.SetHeadCompany(orgDouble, company);
          }
        }
        else
        {
          Logger.DebugFormat("CompaniesCheckDoubles. Для организации {0} (ID={1}) дубли не найдены.", company.Name, company.Id);
        }

        Functions.Module.SetOriginalCompany(company);
      }
      #endregion

      #region Обработка действующих организаций с заполненным ИНН и КПП, которые ранее не были обработаны.
      Logger.Debug("CompaniesCheckDoubles. Обработка действующих организаций с заполненным ИНН и КПП.");

      companies = ClearCPDuplicatesTemplate.Companies.GetAll()
        .Where(c => c.OriginalCompanyDirRX == null && c.Status == Sungero.Parties.Company.Status.Active).ToList()
        .Where(c => !c.ExchangeBoxes.Any() && !c.DoubleStatusDirRX.HasValue && !string.IsNullOrEmpty(c.TIN) && !string.IsNullOrEmpty(c.TRRC));
      Logger.DebugFormat("CompaniesCheckDoubles. Найдено действующих организаций с заполненным ИНН и КПП {0}.", companies.Count());

      foreach (var company in companies)
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка действующей организации {0} (ID={1}) с заполненным ИНН и КПП.", company.Name, company.Id);
        
        // Получаем актуальные данные по организации из БД, т.к. в процессе обработки она могла быть помечена как дубль.
        var actualCompany = ClearCPDuplicatesTemplate.Companies.Get(company.Id);

        if (actualCompany.OriginalCompanyDirRX == null)
        {
          var doubles = Functions.Module.FindAllOrgDoubles(company, false);
          if (doubles?.Count > 0)
          {
            foreach (var orgDouble in doubles)
            {
              Functions.Module.SetHeadCompany(orgDouble, company);
            }
          }
          else
          {
            Logger.DebugFormat("CompaniesCheckDoubles. Для организации {0} (ID={1}) дубли не найдены.", company.Name, company.Id);
          }

          // Проставим признак "Оригинал".
          Functions.Module.SetOriginalCompany(company);
        }
        else
        {
          Logger.DebugFormat("CompaniesCheckDoubles. Организация {0} (ID={1}) обработана ранее и является дублем.", company.Name, company.Id);
        }
      }
      #endregion

      #region Обработка закрытых организаций с заполненным ИНН и КПП, где есть активные дубли.
      Logger.Debug("CompaniesCheckDoubles. Обработка закрытых организаций с заполненным ИНН и КПП, где есть активные дубли.");

      companies = ClearCPDuplicatesTemplate.Companies.GetAll()
        .Where(c => c.OriginalCompanyDirRX == null && c.Status == Sungero.Parties.Company.Status.Closed).ToList()
        .Where(c => !c.ExchangeBoxes.Any() && !c.DoubleStatusDirRX.HasValue && !string.IsNullOrEmpty(c.TIN) && !string.IsNullOrEmpty(c.TRRC));
      Logger.DebugFormat("CompaniesCheckDoubles. Найдено закрытых организаций с заполненным ИНН и КПП, где есть активные дубли {0}.", companies.Count());

      foreach (var company in companies)
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка закрытой организации {0} (ID={1}) с заполненным ИНН и КПП, где есть активные дубли.", company.Name, company.Id);

        // Получаем актуальные данные по организации из БД, т.к. в процессе обработки она могла быть помечена как дубль.
        var actualCompany = ClearCPDuplicatesTemplate.Companies.Get(company.Id);
        if (actualCompany.OriginalCompanyDirRX == null)
        {
          var doubles = Functions.Module.FindAllOrgDoubles(company, false);
          if (doubles?.Count > 0)
          {
            foreach (var orgDouble in doubles)
            {
              Functions.Module.SetHeadCompany(orgDouble, company);
            }
            Functions.Module.SetOriginalCompany(company);
          }
        }
        else
        {
          Logger.DebugFormat("CompaniesCheckDoubles. Организация {0} (ID={1}) обработана ранее и является дублем.", company.Name, company.Id);
        }
      }
      #endregion

      #region Обработка действующих организаций с заполненным ИНН и пустым КПП.
      Logger.Debug("CompaniesCheckDoubles. Обработка действующих организаций с заполненным ИНН и пустым КПП.");

      companies = ClearCPDuplicatesTemplate.Companies.GetAll()
        .Where(c => c.OriginalCompanyDirRX == null && c.Status == Sungero.Parties.Company.Status.Active).ToList()
        .Where(c => !c.ExchangeBoxes.Any() && !c.DoubleStatusDirRX.HasValue && !string.IsNullOrEmpty(c.TIN) && string.IsNullOrEmpty(c.TRRC));
      Logger.DebugFormat("CompaniesCheckDoubles. Найдено действующих организаций с заполненным ИНН и пустым КПП {0}.", companies.Count());

      foreach (var company in companies)
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка действующей организации {0} (ID={1}) с заполненным ИНН и пустым КПП.", company.Name, company.Id);

        var actualCompany = ClearCPDuplicatesTemplate.Companies.Get(company.Id);
        if (actualCompany.OriginalCompanyDirRX == null)
        {
          var doubles = Functions.Module.FindAllOrgDoubles(company, false);
          if (doubles?.Count > 0)
          {
            foreach (var orgDouble in doubles)
            {
              Functions.Module.SetHeadCompany(orgDouble, company);
            }
            Functions.Module.SetOriginalCompany(company);
          }
          else
          {
            Logger.DebugFormat("CompaniesCheckDoubles. Для организации {0} (ID={1}) дубли не найдены.", company.Name, company.Id);
          }
        }
        else
        {
          Logger.DebugFormat("CompaniesCheckDoubles. Организация {0} (ID={1}) обработана ранее и является дублем.", company.Name, company.Id);
        }
      }
      #endregion

      #region Обработка закрытых организаций с заполненным ИНН и пустым КПП.
      Logger.Debug("CompaniesCheckDoubles. Обработка закрытых организаций с заполненным ИНН и пустым КПП.");

      companies = ClearCPDuplicatesTemplate.Companies.GetAll()
        .Where(c => c.OriginalCompanyDirRX == null && c.Status == Sungero.Parties.Company.Status.Closed).ToList()
        .Where(c => !c.ExchangeBoxes.Any() && !c.DoubleStatusDirRX.HasValue && !string.IsNullOrEmpty(c.TIN) && string.IsNullOrEmpty(c.TRRC));
      Logger.DebugFormat("CompaniesCheckDoubles. Найдено закрытых организаций с заполненным ИНН и пустым КПП {0}.", companies.Count());

      foreach (var company in companies)
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка закрытой организации {0} (ID={1}) с заполненным ИНН и пустым КПП.", company.Name, company.Id);

        var actualCompany = ClearCPDuplicatesTemplate.Companies.Get(company.Id);
        if (actualCompany.OriginalCompanyDirRX == null)
        {
          var doubles = Functions.Module.FindAllOrgDoubles(company, false);
          if (doubles?.Count > 0)
          {
            foreach (var orgDouble in doubles)
            {
              Functions.Module.SetHeadCompany(orgDouble, company);
            }
            Functions.Module.SetOriginalCompany(company);
          }
          else
          {
            Logger.DebugFormat("CompaniesCheckDoubles. Для организации {0} (ID={1}) дубли не найдены.", company.Name, company.Id);
          }
        }
        else
        {
          Logger.DebugFormat("CompaniesCheckDoubles. Организация {0} (ID={1}) обработана ранее и является дублем.", company.Name, company.Id);
        }
      }
      #endregion

      #region Отправка заданий Отв. за контрагентов на ручной разбор для оригиналов.
      Logger.Debug("CompaniesCheckDoubles. Отправка заданий Отв. за контрагентов на ручной разбор для оригиналов.");

      companies = ClearCPDuplicatesTemplate.Companies.GetAll()
        .Where(c => c.DoubleStatusDirRX == ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsOriginal).ToList();
      foreach (var company in companies)
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка возможных дублей организации {0} (ID={1}).", company.Name, company.Id);
        
        var doubles = Functions.Module.FindAllOrgPossibleDoubles(company, false);

        if (doubles.Any())
        {
          foreach (var orgDouble in doubles)
          {
            if (!orgDouble.DoubleStatusDirRX.HasValue)
              Functions.Module.SetPossibleDouble(orgDouble);
          }
          
          // Отправка задания.
          try
          {
            var activeText = string.Format("Для организации {0} обнаружены возможные дубли, у которых совпадает \"ИНН\". " +
                                           "Просмотрите записи во вложениях и укажите организацию-оригинал или признак \"Оригинал\"." +
                                           "\nУ организации-оригинала проверьте корректность полей \"Наименование\", \"ИНН\" и \"КПП\"", Hyperlinks.Get(company));
            var subject = "Обработайте возможные дубли для организации " + company.Name;
            var attachments = new List<IEntity>();
            attachments.Add(company);
            
            // Добавить вложения.
            foreach (var orgDouble in doubles)
            {
              attachments.Add(orgDouble);
            }
            
            var assignmentType = Sungero.Workflow.SimpleTask.AssignmentType.Assignment;
            var task = Functions.Module.SendNoticeToResponsibleWithAttachments(subject, activeText, attachments, assignmentType);
            
            Logger.DebugFormat("CompaniesCheckDoubles. Отправка задачи на обработку возможных дублей организации {0} (ID={1}). ИД задачи = {2}",
                               company.Name, company.Id, task.Id);
          }
          catch (Exception ex)
          {
            Logger.ErrorFormat("CompaniesCheckDoubles:  Ошибка отправки задания отвественному за контрагентов. Организация: {0}. Ошибка: {1} StackTrace: {2}",
                               company.Name, ex.Message, ex.StackTrace);
          }
        }
        else
        {
          Logger.DebugFormat("CompaniesCheckDoubles. Для организации {0} (ID={1}) возможные дубли не найдены.", company.Name, company.Id);
        }
      }
      #endregion

      #region Отправка задачи ответственному на заполнение ИНН и КПП.
      Logger.Debug("CompaniesCheckDoubles. Отправка задачи ответственному на заполнение ИНН и КПП.");

      companies = ClearCPDuplicatesTemplate.Companies.GetAll().Where(c => !c.DoubleStatusDirRX.HasValue).ToList();

      // Отправка задания.
      try
      {
        var activeText = "Не удалось обработать дубли организаций. Просмотрите записи во вложениях и заполните корректные поля \"ИНН\" и \"КПП\"";
        var subject = "Заполните ИНН и КПП организаций";
        List<IEntity> attachments = new List<IEntity>();
        attachments.AddRange(companies);
        var assignmentType = Sungero.Workflow.SimpleTask.AssignmentType.Assignment;
        var task = Functions.Module.SendNoticeToResponsibleWithAttachments(subject, activeText, attachments, assignmentType);
        
        Logger.DebugFormat("CompaniesCheckDoubles. Отправка задачи ответственному на заполнение ИНН и КПП. ИД задачи = {0}", task.Id);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("CompaniesCheckDoubles: Ошибка отправки задачи ответственному на заполнение ИНН и КПП. Ошибка: {1} StackTrace: {2}",
                           ex.Message, ex.StackTrace);
      }
      #endregion

      Logger.Debug("CompaniesCheckDoubles. Окончание обработки дублей в справочнике Организации.");
    }
  }
}