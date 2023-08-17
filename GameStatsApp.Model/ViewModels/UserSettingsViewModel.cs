﻿using System;
using GameStatsApp.Model.Data;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace GameStatsApp.Model.ViewModels
{
    public class UserSettingsViewModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string WindowsLiveAuthUrl { get; set; }
        public List<int> AccountTypeIDs { get; set; }        
        public bool? AuthSuccess { get; set; }
    }
}

