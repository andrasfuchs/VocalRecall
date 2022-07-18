using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReadABook
{
	public class DrillSampleSentence
	{
		public string Text;
		public long Difficulty;

		public override string ToString()
		{
			return String.Format("{0,6}", this.Difficulty) + "\t|" + this.Text;
		}
	}
}
