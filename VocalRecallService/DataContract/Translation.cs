using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO.Compression;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace VocalRecallService.DataContract
{
    public class Translation
    {
		public int TranslationId;

        public int OriginalWordId;

		public string RootWord;

		public string PartOfSpeech;

        public int CultureId;

		public string Common;
		
		public string Uncommon;

		public string Rare;

		//public string RawResponse;

		public DateTime LastUpdated;
    }
}