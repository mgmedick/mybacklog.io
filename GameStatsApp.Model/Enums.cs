﻿using System.Runtime.Serialization;

namespace GameStatsApp.Model
{
    public enum Template
    {
        ActivateEmail = 1,
        ResetPasswordEmail = 2,
        ConfirmRegistration = 3
    }       

    public enum GameService
    {
        Steam = 1,
        Xbox = 2
    } 

    public enum DefaultGameList
    {
        Backlog = 1,
        Playing = 2,
        Completed = 3
    } 
}
