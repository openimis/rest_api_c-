﻿using System.ComponentModel.DataAnnotations;

namespace ImisRestApi.Models
{
    public class ControlNumberContainer
    {
        [Required]
        public int PaymentId { get; set; }
        public string ControlNumber { get; set; }
    }
}