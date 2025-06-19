using System.ComponentModel.DataAnnotations;
using System.Security;

namespace Netwolf.Server;

public enum Numeric : int
{
    [Display(Description = "Welcome to the {network.NetworkName} Network, {user.Nickname}")]
    RPL_WELCOME = 1,
    [Display(Description = "Your host is {network.ServerName}, running version {network.Version}")]
    RPL_YOURHOST = 2,
    [Display(Description = "This server was created {DateTime.Now.ToString(\"u\")}")]
    RPL_CREATED = 3,
    // No Description text for this numeric
    RPL_MYINFO = 4,
    [Display(Description = "are supported by this server")]
    RPL_ISUPPORT = 5,
    // No Description text for this numeric
    RPL_UMODEIS = 221,
    [Display(Description = "There are {network.UserCount} users and {network.InvisibleCount} invisible on 1 servers")]
    RPL_LUSERCLIENT = 251,
    [Display(Description = "operator(s) online")]
    RPL_LUSEROP = 252,
    [Display(Description = "unknown connection(s)")]
    RPL_LUSERUNKNOWN = 253,
    [Display(Description = "channels formed")]
    RPL_LUSERCHANNELS = 254,
    [Display(Description = "I have {network.UserCount} clients and 0 servers")]
    RPL_LUSERME = 255,
    [Display(Description = "Administrative info")]
    RPL_ADMINME = 256,
    // No Description text for this numeric
    RPL_ADMINLOC1 = 257,
    // No Description text for this numeric
    RPL_ADMINLOC2 = 258,
    // No Description text for this numeric
    RPL_ADMINEMAIL = 259,
    [Display(Description = "Please wait a while and try again")]
    RPL_TRYAGAIN = 263,
    [Display(Description = "Current local users {network.UserCount}, max {network.MaxUserCount}")]
    RPL_LOCALUSERS = 265,
    [Display(Description = "Current global users {network.UserCount}, max {network.MaxUserCount}")]
    RPL_GLOBALUSERS = 266,
    [Display(Description = "has client certificate fingerprint {user.CertificateFingerprint}")]
    RPL_WHOISCERTFP = 276,
    // No Description text for this numeric
    RPL_NONE = 300,
    // No Description text for this numeric
    RPL_AWAY = 301,
    // No Description text for this numeric
    RPL_USERHOST = 302,
    [Display(Description = "You are no longer marked as being away")]
    RPL_UNAWAY = 305,
    [Display(Description = "You have been marked as being away")]
    RPL_NOWAWAY = 306,
    [Display(Description = "has identified for this nick")]
    RPL_WHOISREGNICK = 307,
    // No Description text for this numeric
    RPL_WHOISUSER = 311,
    // No Description text for this numeric
    RPL_WHOISSERVER = 312,
    [Display(Description = "is an IRC operator")]
    RPL_WHOISOPERATOR = 313,
    // No Description text for this numeric
    RPL_WHOWASUSER = 314,
    [Display(Description = "End of /WHO list")]
    RPL_ENDOFWHO = 315,
    [Display(Description = "seconds idle, signon time")]
    RPL_WHOISIDLE = 317,
    [Display(Description = "End of /WHOIS")]
    RPL_ENDOFWHOIS = 318,
    // No Description text for this numeric
    RPL_WHOISCHANNELS = 319,
    // No Description text for this numeric
    RPL_WHOISSPECIAL = 320,
    [Display(Description = "Users  Name")]
    RPL_LISTSTART = 321,
    // No Description text for this numeric
    RPL_LIST = 322,
    [Display(Description = "End of /LIST")]
    RPL_LISTEND = 323,
    // No Description text for this numeric
    RPL_CHANNELMODEIS = 324,
    // No Description text for this numeric
    RPL_CREATIONTIME = 329,
    [Display(Description = "is logged in as")]
    RPL_WHOISACCOUNT = 330,
    [Display(Description = "No topic is set")]
    RPL_NOTOPIC = 331,
    // No Description text for this numeric
    RPL_TOPIC = 332,
    // No Description text for this numeric
    RPL_TOPICWHOTIME = 333,
    // No Description text for this numeric
    RPL_INVITELIST = 336,
    [Display(Description = "End of /INVITE list")]
    RPL_ENDOFINVITELIST = 337,
    [Display(Description = "Is actually using host")]
    RPL_WHOISACTUALLY = 338,
    // No Description text for this numeric
    RPL_INVITING = 341,
    // No Description text for this numeric
    RPL_INVEXLIST = 346,
    [Display(Description = "End of channel invite exception list")]
    RPL_ENDOFINVEXLIST = 347,
    // No Description text for this numeric
    RPL_EXCEPTLIST = 348,
    [Display(Description = "End of channel exception list")]
    RPL_ENDOFEXCEPTLIST = 349,
    // No Description text for this numeric
    RPL_VERSION = 351,
    // No Description text for this numeric
    RPL_WHOREPLY = 352,
    // No Description text for this numeric
    RPL_NAMREPLY = 353,
    // No Description text for this numeric
    RPL_WHOSPCRPL = 354,
    // No Description text for this numeric
    RPL_LINKS = 364,
    [Display(Description = "End of /LINKS list")]
    RPL_ENDOFLINKS = 365,
    [Display(Description = "End of /NAMES list")]
    RPL_ENDOFNAMES = 366,
    // No Description text for this numeric
    RPL_BANLIST = 367,
    [Display(Description = "End of channel ban list")]
    RPL_ENDOFBANLIST = 368,
    [Display(Description = "End of /WHOWAS")]
    RPL_ENDOFWHOWAS = 369,
    // No Description text for this numeric
    RPL_INFO = 371,
    // No Description text for this numeric
    RPL_MOTD = 372,
    [Display(Description = "End of /INFO list")]
    RPL_ENDOFINFO = 374,
    [Display(Description = "--- {network.Name} Message of the day ---")]
    RPL_MOTDSTART = 375,
    [Display(Description = "End of /MOTD")]
    RPL_ENDOFMOTD = 376,
    [Display(Description = "is connecting from *@{user.RealHost} {user.RealIP}")]
    RPL_WHOISHOST = 378,
    [Display(Description = "is using modes {user.ModeString}")]
    RPL_WHOISMODES = 379,
    [Display(Description = "You are now an IRC operator")]
    RPL_YOUREOPER = 381,
    [Display(Description = "Rehashing")]
    RPL_REHASHING = 382,
    // No Description text for this numeric
    RPL_TIME = 391,
    // No Description text for this numeric
    ERR_UNKNOWNERROR = 400,
    [Display(Description = "No such nick/channel")]
    ERR_NOSUCHNICK = 401,
    [Display(Description = "No such server")]
    ERR_NOSUCHSERVER = 402,
    [Display(Description = "No such channel")]
    ERR_NOSUCHCHANNEL = 403,
    [Display(Description = "Cannot send to channel")]
    ERR_CANNOTSENDTOCHAN = 404,
    [Display(Description = "You have joined too many channels")]
    ERR_TOOMANYCHANNELS = 405,
    [Display(Description = "There was no such nickname")]
    ERR_WASNOSUCHNICK = 406,
    [Display(Description = "No origin specified")]
    ERR_NOORIGIN = 409,
    [Display(Description = "Invalid CAP command")]
    ERR_INVALIDCAPCOMMAND = 410,
    [Display(Description = "Input line was too long")]
    ERR_INPUTTOOLONG = 417,
    [Display(Description = "Unknown command")]
    ERR_UNKNOWNCOMMAND = 421,
    [Display(Description = "MOTD file is missing")]
    ERR_NOMOTD = 422,
    [Display(Description = "No nickname given")]
    ERR_NONICKNAMEGIVEN = 431,
    [Display(Description = "Erroneus nickname")]
    ERR_ERRONEUSNICKNAME = 432,
    [Display(Description = "Nickname is already in use")]
    ERR_NICKNAMEINUSE = 433,
    [Display(Description = "Nickname collision")]
    ERR_NICKCOLLISION = 436,
    [Display(Description = "They aren't on that channel")]
    ERR_USERNOTINCHANNEL = 441,
    [Display(Description = "You're not on that channel")]
    ERR_NOTONCHANNEL = 442,
    [Display(Description = "is already on channel")]
    ERR_USERONCHANNEL = 443,
    [Display(Description = "You have not registered")]
    ERR_NOTREGISTERED = 451,
    [Display(Description = "Not enough parameters")]
    ERR_NEEDMOREPARAMS = 461,
    [Display(Description = "You may not reregister")]
    ERR_ALREADYREGISTERED = 462,
    [Display(Description = "Password incorrect")]
    ERR_PASSWDMISMATCH = 464,
    [Display(Description = "You are banned from this server")]
    ERR_YOUREBANNEDCREEP = 465,
    [Display(Description = "Cannot join channel (+l)")]
    ERR_CHANNELISFULL = 471,
    [Display(Description = "is unknown mode character to me")]
    ERR_UNKNOWNMODE = 472,
    [Display(Description = "Cannot join channel (+i)")]
    ERR_INVITEONLYCHAN = 473,
    [Display(Description = "Cannot join channel (+b)")]
    ERR_BANNEDFROMCHAN = 474,
    [Display(Description = "Cannot join channel (+k)")]
    ERR_BADCHANNELKEY = 475,
    [Display(Description = "Bad channel mask")]
    ERR_BADCHANMASK = 476,
    [Display(Description = "You do not have permission to execute this command")]
    ERR_NOPRIVILEGES = 481,
    [Display(Description = "Permission denied: missing channel privileges")]
    ERR_CHANOPPRIVSNEEDED = 482,
    [Display(Description = "You can't kill a server!")]
    ERR_CANTKILLSERVER = 483,
    [Display(Description = "You can't /OPER from your host")]
    ERR_NOOPERHOST = 491,
    [Display(Description = "Unknown MODE flag")]
    ERR_UMODEUNKNOWNFLAG = 501,
    [Display(Description = "Can't view/change modes for other users")]
    ERR_USERSDONTMATCH = 502,
    [Display(Description = "No help available on this topic")]
    ERR_HELPNOTFOUND = 524,
    [Display(Description = "Key is not well-formed")]
    ERR_INVALIDKEY = 525,
    [Display(Description = "Permission denied: missing oper privileges")]
    ERR_NOPRIVS = 723,
    [Display(Description = "You are now logged in as {user.Account}")]
    RPL_LOGGEDIN = 900,
    [Display(Description = "You are now logged out")]
    RPL_LOGGEDOUT = 901,
    [Display(Description = "You must use a nick assigned to you")]
    ERR_NICKLOCKED = 902,
    [Display(Description = "SASL authentication successful")]
    RPL_SASLSUCCESS = 903,
    [Display(Description = "SASL authentication failed")]
    ERR_SASLFAIL = 904,
    [Display(Description = "SASL message too long")]
    ERR_SASLTOOLONG = 905,
    [Display(Description = "SASL authentication aborted")]
    ERR_SASLABORTED = 906,
    [Display(Description = "You have already authenticated using SASL")]
    ERR_SASLALREADY = 907,
    [Display(Description = "are available SASL mechanisms")]
    RPL_SASLMECHS = 908,
}
