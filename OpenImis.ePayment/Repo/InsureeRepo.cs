﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace OpenImis.ePayment.Repo
{
    public class InsureeRepo : Connection
    {
        public InsureeRepo(IConfiguration configuration) : base(configuration)
        {
        }
    }
}
