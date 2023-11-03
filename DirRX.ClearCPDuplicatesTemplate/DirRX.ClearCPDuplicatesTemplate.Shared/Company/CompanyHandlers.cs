using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using DirRX.ClearCPDuplicatesTemplate.Company;

namespace DirRX.ClearCPDuplicatesTemplate
{
  partial class CompanySharedHandlers
  {

    public virtual void DoubleStatusDirRXChanged(Sungero.Domain.Shared.EnumerationPropertyChangedEventArgs e)
    {
      if (e.NewValue != e.OldValue)
      {
        // Если указали признак "Оригинал" - значит действующая.
        if (e.NewValue == DoubleStatusDirRX.IsOriginal)
        {
          _obj.Status = Status.Active;
        }
        
        // Если указали признак "Дубль" - значит закрытая.
        if (e.NewValue == DoubleStatusDirRX.IsDouble)
        {
          _obj.Status = Status.Closed;
        }
      }
    }

  }
}