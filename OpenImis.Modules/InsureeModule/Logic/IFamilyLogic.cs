﻿using OpenImis.Modules.InsureeModule.Models;
using OpenImis.Modules.InsureeModule.Models.EnrollFamilyModels;
using System;

namespace OpenImis.Modules.InsureeModule.Logic
{
    public interface IFamilyLogic
    {
        FamilyModel GetByCHFID(string chfid, Guid userUUID);
        int Create(EnrollFamilyModel model, int userId, int officerId);
        int GetUserIdByUUID(Guid uuid);
        int GetOfficerIdByUserUUID(Guid userUUID);
    }
}
