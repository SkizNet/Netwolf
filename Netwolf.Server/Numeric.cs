using System.ComponentModel.DataAnnotations;

namespace Netwolf.Server
{
    public enum Numeric : int
    {
        [Display(Description = "Welcome to the {network.NetworkName} Network, {state.Nickname}!")]
        RPL_WELCOME = 1,
        [Display(Description = "Your host is {network.ServerName}, running version {network.Version}")]
        RPL_YOURHOST = 2,
        [Display(Description = "This server was created {DateTime.Now.ToString(\"G\")}")]
        RPL_CREATED = 3,
        // No Description text for this numeric
        RPL_MYINFO = 4,
        [Display(Description = "are supported by this server")]
        RPL_ISUPPORT = 5,
        // No Description text for this numeric
        RPL_UMODEIS = 221,
        [Display(Description = "Unknown command")]
        ERR_UNKNOWNCOMMAND = 421,
        [Display(Description = "No nickname given")]
        ERR_NONICKNAMEGIVEN = 431,
        [Display(Description = "Erroneus nickname")]
        ERR_ERRONEUSNICKNAME = 432,
        [Display(Description = "Nickname is already in use")]
        ERR_NICKNAMEINUSE = 433,
        [Display(Description = "Nickname collision")]
        ERR_NICKCOLLISION = 436,
        [Display(Description = "Not enough parameters")]
        ERR_NEEDMOREPARAMS = 461,
        [Display(Description = "You may not reregister")]
        ERR_ALREADYREGISTERED = 462,
    }
}