using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using ReadABook.VocalRecallService;

namespace ReadABook
{
    class CaseInsensitiveWordEquityComparer : IEqualityComparer<Word>, IComparer<Word>
    {
        CaseInsensitiveComparer caseInsensitiveComparer = new CaseInsensitiveComparer();

        #region IEqualityComparer<Word> Members

        public bool Equals(Word x, Word y)
        {
            return caseInsensitiveComparer.Compare(x.Text, y.Text) == 0;
        }

        public int GetHashCode(Word obj)
        {
            return obj.Text.GetHashCode();
        }

        #endregion

		public int Compare(Word x, Word y)
		{
			return caseInsensitiveComparer.Compare(x.Text, y.Text);
		}
	}
}
