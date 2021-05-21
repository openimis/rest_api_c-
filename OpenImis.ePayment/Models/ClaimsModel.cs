﻿using OpenImis.ePayment.ImisAttributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OpenImis.ePayment.Models
{
    public class ClaimsModel
    {
        [Required]
        public string claim_administrator_code { get; set; }
        public ClaimStatus status_claim { get; set; }
        [ValidDate(ErrorMessage = "4:Wrong or missing enrolment date")]
        public string visit_date_from { get; set; }
        [ValidDate(ErrorMessage = "4:Wrong or missing enrolment date")]
        public string visit_date_to { get; set; }
        [ValidDate(ErrorMessage = "4:Wrong or missing enrolment date")]
        public string processed_date_from { get; set; }
        [ValidDate(ErrorMessage = "4:Wrong or missing enrolment date")]
        public string processed_date_to { get; set; }
    }

    public enum ClaimStatus
    {
        Rejected = 1,
        Entered = 2,
        Checked = 4,
        Processed = 8,
        Valuated = 16
    }
}