using ReadABook.VocalRecallService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReadABook
{
	public class LevelStatEntry
	{
		public Word Word;
		public int LocalFrequency;
		public double Percentage;

		public override string ToString()
		{
			return this.LocalFrequency.ToString("N0") + "|" + this.Word.Text + " (" + this.Word.Index.ToString("N0") + ")";
		}
	}
}
