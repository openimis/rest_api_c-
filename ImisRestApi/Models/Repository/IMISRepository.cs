﻿using ImisRestApi.Models.Interfaces;
using ImisRestApi.Models.TanzaniaRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImisRestApi.Models.Repository
{
    /// <summary>
    /// This Service instanciates the Entities repositories and serves as an entry point to these 
    /// </summary>
    public class IMISRepository: IIMISRepository
    {
        private readonly IUserRepository userRepository;
        public IMISRepository()
        {
            userRepository = new UserRepository();
        }

        /// <summary>
        /// Return the user repository
        /// </summary>
        /// <returns>
        /// The instance of the user repository
        /// </returns>
        public IUserRepository getUserRepository()
        {
            return userRepository;
        }
    }
}
