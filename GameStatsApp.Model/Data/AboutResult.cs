﻿using System;
using GameStatsApp.Model.Data;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using GameStatsApp.Model;

namespace GameStatsApp.Model.Data
{
    public class AboutResult
    {
        public int UserCount { get; set; }
        public string GameName { get; set; }
        public string UserListName { get; set; }
    }
}

