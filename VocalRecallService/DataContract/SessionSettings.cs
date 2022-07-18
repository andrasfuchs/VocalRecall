using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Runtime.Serialization;

namespace VocalRecallService.DataContract
{
    [DataContract]
    public class SessionSettings
    {
        [DataMember]
        public int SessionId;

        [DataMember]
        public int LanguageToLearnId;
    }
}