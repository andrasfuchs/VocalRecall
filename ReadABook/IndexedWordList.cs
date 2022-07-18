using ReadABook.VocalRecallService;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReadABook
{
	[Serializable]
	public class IndexedWordList
	{
		private Dictionary<int, Dictionary<char, List<Word>>> wordBuckets = new Dictionary<int, Dictionary<char, List<Word>>>();
		private CaseInsensitiveComparer caseInsensitiveComparer = new CaseInsensitiveComparer();

		public long Count
		{
			get
			{
				long i = 0;

				foreach (Dictionary<char, List<Word>> b in wordBuckets.Values)
				{
					foreach (List<Word> wl in b.Values)
					{
						i += wl.Count;
					}
				}

				return i;
			}
		}

		public long TotalNumberOfWords
		{
			get
			{
				long i = 0;

				foreach (Dictionary<char, List<Word>> b in wordBuckets.Values)
				{
					foreach (List<Word> wl in b.Values)
					{
						i += wl.Sum(w => w.Frequency.Value);
					}
				}

				return i;
			}
		}

		public Word Find(string text, bool caseSensitive = false)
		{
			int wordLength = text.Length;
			if (!wordBuckets.ContainsKey(wordLength)) return null;

			char wordFirstChar = Char.ToLower(text[0]);
			if (!wordBuckets[wordLength].ContainsKey(wordFirstChar)) return null;

			if (caseSensitive)
			{
				return wordBuckets[wordLength][wordFirstChar].Find(w => text == w.Text);
			}
			else
			{
				return wordBuckets[wordLength][wordFirstChar].Find(w => caseInsensitiveComparer.Compare(text, w.Text) == 0);
			}
		}

		public void Add(string text, int frequency = 1)
		{
			int wordLength = text.Length;
			if (!wordBuckets.ContainsKey(wordLength))
			{
				wordBuckets.Add(wordLength, new Dictionary<char, List<Word>>());
			}

			char wordFirstChar = Char.ToLower(text[0]);
			if (!wordBuckets[wordLength].ContainsKey(wordFirstChar))
			{
				wordBuckets[wordLength].Add(wordFirstChar, new List<Word>());
			}

			Word newWord = new Word() { Text = text, Frequency = frequency };
			newWord.FrequencyLastUpdated = DateTime.UtcNow;
			wordBuckets[wordLength][wordFirstChar].Add(newWord);
		}

		public void Add(Word word)
		{
			int wordLength = word.Text.Length;
			if (!wordBuckets.ContainsKey(wordLength))
			{
				wordBuckets.Add(wordLength, new Dictionary<char, List<Word>>());
			}

			char wordFirstChar = Char.ToLower(word.Text[0]);
			if (!wordBuckets[wordLength].ContainsKey(wordFirstChar))
			{
				wordBuckets[wordLength].Add(wordFirstChar, new List<Word>());
			}

			wordBuckets[wordLength][wordFirstChar].Add(word);
		}

		public void Append(string text, int frequency = 1, bool caseSensitive = false)
		{
			Word w = this.Find(text, caseSensitive);

			if (w == null)
			{
				Add(text, frequency);
			}
			else
			{
				w.Frequency += frequency;
				w.FrequencyLastUpdated = DateTime.UtcNow;
			}
		}

		public void CopyAll(List<Word> words)
		{
			foreach (Dictionary<char, List<Word>> b in wordBuckets.Values)
			{
				foreach (List<Word> wl in b.Values)
				{
					words.AddRange(wl);
				}
			}
		}

		public void MergeWith(IndexedWordList wordData, bool caseSensitive = false)
		{
			List<Word> wl = new List<Word>();
			wordData.CopyAll(wl);

			foreach (Word w in wl)
			{
				this.Append(w.Text, w.Frequency.Value, caseSensitive);
			}
		}

		public void RemoveUnfrequentWords(int minimumFrequency)
		{
			foreach (Dictionary<char, List<Word>> b in wordBuckets.Values)
			{
				foreach (List<Word> wl in b.Values)
				{
					List<Word> wordsToRemove = new List<Word>();

					foreach (Word w in wl)
					{
						if (w.Frequency < minimumFrequency)
						{
							wordsToRemove.Add(w);
						}
					}

					foreach (Word w in wordsToRemove)
					{
						wl.Remove(w);
					}
				}
			}
		}
	}
}
