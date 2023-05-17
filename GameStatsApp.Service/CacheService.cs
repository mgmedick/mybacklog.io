﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mail;
using System.Net;
using Serilog;
using SpeedRunApp.Interfaces.Services;
using SpeedRunApp.Interfaces.Repositories;
using System.Threading.Tasks;
using SpeedRunApp.Model.Data;
using SpeedRunApp.Model;
using System.Linq.Expressions;

namespace SpeedRunApp.Service
{
    public class CacheService : ICacheService
    {
        public IMemoryCache _cache { get; set; }
        public IUserRepository _userRepo { get; set; }
        public IGameRepository _gameRepo { get; set; }
        public ISpeedRunRepository _speedRunRepo { get; set; }
        public CacheService(IMemoryCache cache, IUserRepository userRepo, IGameRepository gameRepo, ISpeedRunRepository speedRunRepo)
        {
            _cache = cache;
            _userRepo = userRepo;
            _gameRepo = gameRepo;
            _speedRunRepo = speedRunRepo;
        }

        public IEnumerable<User> GetUsers()
        {
            IEnumerable<User> users = null;
            if (!_cache.TryGetValue<IEnumerable<User>>("users", out users))
            {
                users = _userRepo.GetUsers();
                _cache.Set("users", users);
            }

            return users;
        }
    }
}
