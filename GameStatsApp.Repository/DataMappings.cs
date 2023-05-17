﻿using System;
using System.Collections.Generic;
using System.Text;
using NPoco.FluentMappings;
using GameStatsApp.Model.Data;

namespace SpeedRunApp.Repository
{
    public class DataMappings : Mappings
    {
        public DataMappings()
        {
            For<User>().PrimaryKey("ID").TableName("tbl_User");
            For<UserSetting>().PrimaryKey("UserID", false).TableName("tbl_User_Setting");
            For<UserView>().TableName("vw_User");
            For<Setting>().PrimaryKey("ID").TableName("tbl_Setting");
        }
    }
}



