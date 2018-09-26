﻿using ImisRestApi.Chanels.Payment.Models;
using ImisRestApi.ImisAttributes;
using System.ComponentModel.DataAnnotations;

namespace ImisRestApi.Models
{
    public class PaymentDetail
    {
        [InsureeNumber]
        public string InsureeNumber { get; set; }
        [Required(ErrorMessage = "003:Product Code was not provided")]
        public string ProductCode { get; set; }
        [Required(ErrorMessage = "004:EnrolmentType was not provided")]
        public EnrolmentType PaymentType { get; set; }
        public int IsRenewal()
        {
            switch (PaymentType)
            {
                case EnrolmentType.Renewal:
                    return 1;
                case EnrolmentType.New:
                    return 0;
                default:
                    return 0;
            }
        }
    }
}