using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using DirRX.ClearCPDuplicatesTemplate.Company;

namespace DirRX.ClearCPDuplicatesTemplate
{
  partial class CompanyCreatingFromServerHandler
  {

    public override void CreatingFrom(Sungero.Domain.CreatingFromEventArgs e)
    {
      base.CreatingFrom(e);
      e.Without(_info.Properties.OriginalCompanyDirRX);
      e.Without(_info.Properties.DoubleStatusDirRX);      
    }
  }

  partial class CompanyOriginalCompanyDirRXPropertyFilteringServerHandler<T>
  {

    public virtual IQueryable<T> OriginalCompanyDirRXFiltering(IQueryable<T> query, Sungero.Domain.PropertyFilteringEventArgs e)
    {
      query = query.Where(c => c.DoubleStatusDirRX == DoubleStatusDirRX.IsOriginal && c.Id != _obj.Id);
      return query;
    }
  }

  partial class CompanyFilteringServerHandler<T>
  {
    
  }

  partial class CompanyServerHandlers
  {
    public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
    {
      base.BeforeSave(e);
      // Если указали организацию-оригинал, значит установим статус "Дубль".
      if (_obj.OriginalCompanyDirRX != null && _obj.DoubleStatusDirRX != DoubleStatusDirRX.IsDouble)
        _obj.DoubleStatusDirRX = DoubleStatusDirRX.IsDouble;
      
      // Если установлен статус "Дубль", то поле Оригинал организации становится обязательным для заполнения.
      if (_obj.DoubleStatusDirRX == Company.DoubleStatusDirRX.IsDouble && _obj.OriginalCompanyDirRX == null)
        e.AddError(DirRX.ClearCPDuplicatesTemplate.Companies.Resources.OriginalCompanyNotSelectedError);
    }
  }
}