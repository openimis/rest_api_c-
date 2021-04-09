﻿using System;
using System.Collections.Generic;
using System.Text;

namespace OpenImis.Modules.FeedbackModule.Models
{
    public class FeedbackRequest
    {
        public string Officer { get; set; }
        public Guid ClaimUUID { get; set; }
        public string CHFID { get; set; }
        public string Answers { get; set; }
        public string Date { get; set; }
    }
}
