﻿using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace ImisRestApi.Models.Entities
{
    public partial class TblUsers
    {
        public TblUsers()
        {
            TblClaim = new HashSet<TblClaim>();
            TblLogins = new HashSet<TblLogins>();
            TblUsersDistricts = new HashSet<TblUsersDistricts>();
        }

        public int UserId { get; set; }
        public string LanguageId { get; set; }
        public string LastName { get; set; }
        public string OtherNames { get; set; }
        public string Phone { get; set; }
        public string LoginName { get; set; }
        public int RoleId { get; set; }
        public int? Hfid { get; set; }
        public DateTime ValidityFrom { get; set; }
        public DateTime? ValidityTo { get; set; }
        public int? LegacyId { get; set; }
        public int AuditUserId { get; set; }
        public byte[] Password { get; set; }
        public string DummyPwd { get; set; }
        public string EmailId { get; set; }
        public string PrivateKey { get; set; }



        public TblLanguages Language { get; set; }
        public ICollection<TblClaim> TblClaim { get; set; }
        public ICollection<TblLogins> TblLogins { get; set; }
        public ICollection<TblUsersDistricts> TblUsersDistricts { get; set; }

        public Dictionary<int, string> rolesMapping = new Dictionary<int, string>
        {
            {1, "EnrollmentOfficer"},
            {2, "Manager"},
            {4, "Accountant"},
            {8, "Clerk"},
            {16, "MedicalOfficer"},
            {32, "SchemeAdmin"},
            {64, "IMISAdmin" },
            {128, "Receptionist"},
            {256, "ClaimAdmin"},
            {512, "ClaimContrib"},
            {524288, "HFAdmin"},
            {1048576, "OfflineSchemeAdmin"}
        };

        public bool CheckPassword(string password)
        {
            if (this.Password.ToString() == password)
            {
                return true;
            }

            return false;
        }

        public String[] GetRolesStringArray()
        {
            var roles = new List<String> { };
            foreach (KeyValuePair<int, string> role in rolesMapping)
            {
                if ((this.RoleId & role.Key) == role.Key)
                {
                    roles.Add(role.Value);
                }
            }
            
            return roles.ToArray();
        }

        public String NewPrivateKey()
        {
            this.PrivateKey = Guid.NewGuid().ToString();
            return this.PrivateKey;
        }
    }
}
