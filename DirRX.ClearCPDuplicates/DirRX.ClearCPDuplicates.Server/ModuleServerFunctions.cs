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
  public class ModuleFunctions
  {
    
    /// <summary>
    /// Заполняет у дубля организации поле Оригинал организации.
    /// </summary>
    /// <param name="orgDouble">Дубль организации.</param>
    /// <param name="company">Оригинал организации.</param>
    public void SetHeadCompany(ClearCPDuplicatesTemplate.ICompany orgDouble, ClearCPDuplicatesTemplate.ICompany company)
    {
      try
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка дубля организации ID={0}. Дубль {1} (ID={2}).", company.Id, orgDouble.Name, orgDouble.Id);
        orgDouble.OriginalCompanyDirRX = company;
        orgDouble.DoubleStatusDirRX = ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsDouble;
        orgDouble.Status = Sungero.Parties.Company.Status.Closed;
        Transactions.Execute(() => orgDouble.Save());
        Logger.DebugFormat("CompaniesCheckDoubles. Сохранение дубля организации ID={0}. Дубль {1} (ID={2}).", company.Id, orgDouble.Name, orgDouble.Id);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("CompaniesCheckDoubles. Ошибка сохранения дубля организации ID={0}: {1} StackTrace: {2}", orgDouble.Id, ex.Message, ex.StackTrace);
      }
    }

    /// <summary>
    /// Установка признака оригинальной организации.
    /// </summary>
    /// <param name="company">Организация.</param>
    public void SetOriginalCompany(ClearCPDuplicatesTemplate.ICompany company)
    {
      try
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка оригинальной организации {0} (ID={1}).", company.Name, company.Id);
        company.DoubleStatusDirRX = ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsOriginal;
        Transactions.Execute(() => company.Save());
        Logger.DebugFormat("CompaniesCheckDoubles. Сохранение организации {0} (ID={1}).", company.Name, company.Id);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("CompaniesCheckDoubles. Ошибка сохранения организации ID={0}: {1} StackTrace: {2}", company.Id, ex.Message, ex.StackTrace);
      }
    }
    
    /// <summary>
    /// Установка признака Возможный дубль.
    /// </summary>
    /// <param name="company">Организация.</param>
    public void SetPossibleDouble(ClearCPDuplicatesTemplate.ICompany company)
    {
      try
      {
        Logger.DebugFormat("CompaniesCheckDoubles. Обработка возможного дубля {0} (ID={1}).", company.Name, company.Id);
        company.DoubleStatusDirRX = ClearCPDuplicatesTemplate.Company.DoubleStatusDirRX.IsPossibleDoubl;
        Transactions.Execute(() => company.Save());
        Logger.DebugFormat("CompaniesCheckDoubles. Сохранение организации {0} (ID={1}).", company.Name, company.Id);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("CompaniesCheckDoubles. Ошибка сохранения организации ID={0}: {1} StackTrace: {2}", company.Id, ex.Message, ex.StackTrace);
      }
    }

    /// <summary>
    /// Поиск полных дублей организаций.
    /// </summary>
    /// <param name="company">Организация, для которой выполняется поиск дублей</param>
    /// <param name="onlyActive">Поиск дублей только по действующим организациям</param>
    /// <returns>Список дублей организаций</returns>
    public virtual List<ClearCPDuplicatesTemplate.ICompany> FindAllOrgDoubles(ClearCPDuplicatesTemplate.ICompany company, bool onlyActive)
    {
      var companies = ClearCPDuplicatesTemplate.Companies.GetAll(c => c.Id != company.Id && c.OriginalCompanyDirRX == null && !c.DoubleStatusDirRX.HasValue);

      if (onlyActive)
        companies = companies.Where(c => c.Status == Sungero.Parties.Company.Status.Active);

      // ИНН + КПП.
      if (!string.IsNullOrEmpty(company.TIN))
      {
        var findOrgDoublesResult = companies.Where(c => c.TIN == company.TIN && c.TRRC == company.TRRC);
        return findOrgDoublesResult.ToList();
      }
      
      return null;
    }
    
    /// <summary>
    /// Поиск возможных дублей организаций.
    /// </summary>
    /// <param name="company">Организация, для которой выполняется поиск дублей</param>
    /// <param name="onlyActive">Поиск дублей только по действующим организациям</param>
    /// <returns>Список дублей организаций</returns>
    public virtual List<ClearCPDuplicatesTemplate.ICompany> FindAllOrgPossibleDoubles(ClearCPDuplicatesTemplate.ICompany company, bool onlyActive)
    {
      var companies = ClearCPDuplicatesTemplate.Companies.GetAll(c => c.Id != company.Id && c.OriginalCompanyDirRX == null && !c.DoubleStatusDirRX.HasValue);

      if (onlyActive)
        companies = companies.Where(c => c.Status == Sungero.Parties.Company.Status.Active);

      // ИНН + КПП.
      var findOrgPossibleDoublesResult = companies.Where(c => c.TIN == company.TIN && c.TRRC != company.TRRC);
      return findOrgPossibleDoublesResult.ToList();
    }

    /// <summary>
    /// Отправить задачу отвественному за контрагентов.
    /// </summary>
    /// <param name="subject">Тема задания.</param>
    /// <param name="text">Текст задания.</param>
    /// <param name="entityList">Список вложений.</param>
    /// <param name="assignmentType">Тип задания.</param>
    /// <returns> Стартованная задача.</returns>
    [Public]
    public Sungero.Workflow.ISimpleTask SendNoticeToResponsibleWithAttachments(string subject, string text, List<IEntity> entityList, Sungero.Core.Enumeration assignmentType)
    {
      Logger.DebugFormat("SendNoticeToResponsibleWithAttachments:  Отправка задачи отвественному за контрагентов. Тема: {0}", subject);
      
      try
      {
        var performer = Roles.GetAll(r => r.Sid == Sungero.Docflow.PublicConstants.Module.RoleGuid.CounterpartiesResponsibleRole).FirstOrDefault();
        var task = Sungero.Workflow.SimpleTasks.Create();
        task.ActiveText = text;
        task.Subject = subject;
        foreach (var attachment in entityList)
        {
          task.Attachments.Add(attachment);
        }
        var step = task.RouteSteps.AddNew();
        step.Performer = performer;
        step.AssignmentType = assignmentType;
        task.NeedsReview = false;
        // Транзакция для обхода наведенной ошибки "Транзакция откатилась" при сохранении.
        Transactions.Execute(() => task.Start());
        return task;
      }
      catch (Exception ex)
      {
        Logger.DebugFormat("SendNoticeToResponsibleWithAttachments:  Ошибка отправки задачи отвественному. Тема: {0}. Ошибка: {1} StackTrace: {2}",
                           subject, ex.Message, ex.StackTrace);
        return null;
      }

    }
  }
}