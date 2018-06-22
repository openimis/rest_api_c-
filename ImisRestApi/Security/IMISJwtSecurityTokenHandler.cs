﻿using ImisRestApi.Models.Entities;
using ImisRestApi.Models.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace ImisRestApi.Security
{
    /// <summary>
    /// This class replaces the default JWT token handler and is used to construct a custom token validator 
    /// </summary>
    public class IMISJwtSecurityTokenHandler : ISecurityTokenValidator
    {
        private int _maxTokenSizeInBytes = TokenValidationParameters.DefaultMaximumTokenSizeInBytes;
        private readonly JwtSecurityTokenHandler _tokenHandler;
        private readonly IIMISRepository _imisRepository;
        
        public IMISJwtSecurityTokenHandler(IServiceCollection serviceCollection)
        {
            _tokenHandler = new JwtSecurityTokenHandler();
            _imisRepository = serviceCollection.BuildServiceProvider().GetService<IIMISRepository>();
        }

        public bool CanValidateToken
        {
            get
            {
                return true;
            }
        }

        public int MaximumTokenSizeInBytes
        {
            get
            {
                return _maxTokenSizeInBytes;
            }

            set
            {
                _maxTokenSizeInBytes = value;
            }
        }

        public bool CanReadToken(string securityToken)
        {
            return _tokenHandler.CanReadToken(securityToken);
        }

        /// <summary>
        /// Validated tokens based on user's private key
        /// </summary>
        /// <param name="securityToken"></param>
        /// <param name="validationParameters"></param>
        /// <param name="validatedToken"></param>
        /// <returns>ClaimsPrincipal</returns>
        public ClaimsPrincipal ValidateToken(string securityToken, TokenValidationParameters validationParameters, out SecurityToken validatedToken)
        {
            ClaimsPrincipal principal;

            var handler = new JwtSecurityTokenHandler();
            var tokenS = handler.ReadToken(securityToken) as JwtSecurityToken;
            var username = tokenS.Claims.First(claim => claim.Type == ClaimTypes.Name).Value;

            var serviceCollection = new ServiceCollection();

            IUserRepository userRepository = _imisRepository.getUserRepository();

            TblUsers user = userRepository.GetByUsername(username);

            if (user != null)
            {

                TokenValidationParameters tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = validationParameters.ValidateIssuer,
                    ValidateAudience = validationParameters.ValidateAudience,
                    ValidateLifetime = validationParameters.ValidateLifetime,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = validationParameters.ValidIssuer,
                    ValidAudience = validationParameters.ValidAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(user.PrivateKey))
                };

                principal = _tokenHandler.ValidateToken(securityToken, tokenValidationParameters, out validatedToken);

            }
            else
            {
                principal = _tokenHandler.ValidateToken(securityToken, validationParameters, out validatedToken);
            }

            return principal;
        }
    }
}
