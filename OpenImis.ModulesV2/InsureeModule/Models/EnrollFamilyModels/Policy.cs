﻿using System;
using System.Collections.Generic;
using System.Text;

namespace OpenImis.ModulesV2.InsureeModule.Models.EnrollFamilyModels
{
    public class Policy
    {
        public int PolicyId { get; set; }
        public int FamilyId { get; set; }
        public DateTime EnrollDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int PolicyStatus { get; set; }
        public decimal PolicyValue { get; set; }
        public int ProdId { get; set; }
        public int OfficerId { get; set; }
        public int PolicyStage { get; set; }
        public bool isOffline { get; set; }
    }
}
