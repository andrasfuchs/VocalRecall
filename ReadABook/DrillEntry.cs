using ReadABook.VocalRecallService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReadABook
{
	public class DrillEntry
	{
		public Word Word;
		public long MaxDifficulty;
		public List<DrillSampleSentence> Samples;
		public Translation[] Translations;
	}
}
