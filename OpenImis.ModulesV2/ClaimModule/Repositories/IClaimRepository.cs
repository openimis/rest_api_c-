﻿using OpenImis.ModulesV2.ClaimModule.Models.RegisterClaim;

namespace OpenImis.ModulesV2.ClaimModule.Repositories
{
    public interface IClaimRepository
    {
        int RegisterClaim(Claim claim);
    }
}