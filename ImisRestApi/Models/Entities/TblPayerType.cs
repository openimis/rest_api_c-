﻿using System;
using System.Collections.Generic;

namespace ImisRestApi.Models.Entities
{
    public partial class TblPayerType
    {
        public TblPayerType()
        {
            TblPayer = new HashSet<TblPayer>();
        }

        public string Code { get; set; }
        public string PayerType { get; set; }
        public string AltLanguage { get; set; }
        public int? SortOrder { get; set; }

        public ICollection<TblPayer> TblPayer { get; set; }
    }
}
