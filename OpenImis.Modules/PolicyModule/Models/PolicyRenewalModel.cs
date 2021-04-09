﻿using System;
using System.Collections.Generic;
using System.Text;

namespace OpenImis.Modules.PolicyModule.Models
{
    public class PolicyRenewalModel
    {
        public int RenewalId { get; set; }
        public string Officer { get; set; }
        public string CHFID { get; set; }
        public string ReceiptNo { get; set; }
        public string ProductCode { get; set; }
        public float Amount { get; set; }
        public string Date { get; set; }
        public bool Discontinue { get; set; }
        public int PayerId { get; set; }


        public Policy GetPolicy()
        {
            return new Policy()
            {
                RenewalId = RenewalId,
                Officer = Officer,
                CHFID = CHFID,
                ReceiptNo = ReceiptNo,
                ProductCode = ProductCode,
                Amount = Amount,
                Date = Date,
                Discontinue = Discontinue,
                PayerId = PayerId
            };
        }
    }
}
