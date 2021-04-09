﻿using System;
using System.Collections.Generic;

namespace OpenImis.DB.SqlServer
{
    public partial class TblLogins
    {
        public int LoginId { get; set; }
        public int? UserId { get; set; }
        public DateTime? LogTime { get; set; }
        public int? LogAction { get; set; }

        public TblUsers User { get; set; }
    }
}
