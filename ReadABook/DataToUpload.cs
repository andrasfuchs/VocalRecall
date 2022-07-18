using ReadABook.VocalRecallService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReadABook
{
	[Serializable]
	public class DataToUpload
	{
		public long TotalBookCount;
		public long TotalWordsProcessed;
		public IndexedWordList Words;
		public long TotalWordCount;
	}
}
