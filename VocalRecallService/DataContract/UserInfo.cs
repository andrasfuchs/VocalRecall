using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Runtime.Serialization;

namespace VocalRecallService.DataContract
{
    [DataContract]
    public class UserInfo
    {
        [DataMember]
        public string Username;

        [DataMember]
        public int CultureId;

        [DataMember]
        public int SessionId;

        [DataMember]
        public int CurrentLevel;
    }
}