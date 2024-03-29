// -r de_DE S:\Books\de_DE\
// -t de-DE en-US 5
// -l de-DE S:\Books\de_DE\
// -d de-DE S:\Books\de_DE\ drillwords.txt samplesentences.txt

using System;
using System.IO;
using System.Collections;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using NHunspell;
using ReadABook.VocalRecallService;

namespace ReadABook
{
    /// <summary>
    /// Summary description for MainClass.
    /// </summary>
    class MainClass
    {
		private static char[] whiteSpaces = new char[] { ' ', ',', '.', '!', '?', ')', '(', '"', '&', ';', '\'', '[', ']', ':', '\\', '_', '`', '„', '<', '>', '\r', '\n', '”', '»', '«', '›', '‹', '\t', '…', '/', '-', '•', '“', '’' };
        private static List<char> vowels = new List<char>() { 'y', 'a', 'e', 'i', 'o', 'u', 'á', 'é', 'ő', 'ú', 'ű', 'ö', 'ü', 'ó', 'í' };
        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length <= 1) 
            {
				Console.WriteLine("Read books");
                Console.WriteLine("usage: readabook.exe -r <culture> <directory of UTF-8 encoded text file(s) with .txt extension>");
                Console.WriteLine("eg. readabook.exe -r en_US ./Books_en_US/");
				Console.WriteLine("    reads all utf-8 txt books in that folder and runs an English spell-checking and save the words into the database");
				Console.WriteLine();
				Console.WriteLine("Download translations");
				Console.WriteLine("usage: readabook.exe -t <original culture> <target culture> <top x words>");
				Console.WriteLine("eg. readabook.exe -t de-DE en-US 2000");
				Console.WriteLine("    tranlates the 2000 most frequent German words to English and saves the translation into the database");
				Console.WriteLine();
				Console.WriteLine("Compute required language level for books");
				Console.WriteLine("usage: readabook.exe -l <culture> <directory of UTF-8 encoded text file(s) with .txt extension>");
				Console.WriteLine("eg. readabook.exe -l hu-HU ./Books_hu_HU/");
				Console.WriteLine("    reads all the books in the directory and creates a small metadata file not to each with statistical information");
				Console.WriteLine();
				Console.WriteLine("Drill one or more words, and get examples for them from the books");
				Console.WriteLine("usage: readabook.exe -d <culture> <directory of UTF-8 encoded text file(s) with .txt extension> <file containing the words to drill> <output csv file with the samples> <target difficulty>");
				Console.WriteLine("eg. readabook.exe -d de-DE ./Books_de_DE/ drillwords.txt samplesentences.txt 1000");
				Console.WriteLine("    reads the books in the directory, looks for the word listed in the drill file and creates a sample file with sample sentences with those words");
				return;
            }

			if (args[0] == "-r")
			{
				ReadBooks(args);
			}
			else if (args[0] == "-t")
			{
				TranslateWords(args);
			}
			else if (args[0] == "-l")
			{
				LevelBooks(args);
			}
			else if (args[0] == "-d")
			{
				DrillWords(args);
			}
			else
			{
				Console.WriteLine("Argument '{0}' is not a valid operation.", args[0]);
			}
        }

		private static void DrillWords(string[] args)
		{
			const int minSamplesPerWord = 10;
			const int maxSamplesPerWord = 15;
			int targetDifficultyLevel = Int32.Parse(args[5]);

			VocalRecallServiceClient vrsc = new VocalRecallServiceClient();
			vrsc.Open();

			Console.WriteLine("Loading word list of top 200'000 words...");
			Culture sourceCulture = vrsc.ListCultures().FirstOrDefault(c => c.CultureIso == args[1]);
			Word[] allWords = vrsc.GetTopWords(sourceCulture.CultureId, 200000);

			IndexedWordList indexedWords = new IndexedWordList();
			foreach (Word w in allWords)
			{
				indexedWords.Add(w);
			}


			string[] bookFiles = Directory.GetFiles(Path.GetFullPath(args[2]), "*.txt");
			string drillFilename = args[3];
			string samplesFilename = args[4];

			Dictionary<string, DrillEntry> drills = new Dictionary<string, DrillEntry>();
			string[] drillWords = File.ReadAllLines(drillFilename);

			foreach (string dw in drillWords)
			{
				if (String.IsNullOrEmpty(dw)) continue;

				Word w = indexedWords.Find(dw);
				if (w == null) continue;

				if (!drills.ContainsKey(w.Text))
				{
					drills.Add(w.Text, new DrillEntry());
					drills[w.Text].Word = w;
					drills[w.Text].MaxDifficulty = Math.Max(5000, w.Index);
					drills[w.Text].Samples = new List<DrillSampleSentence>();
					drills[w.Text].Translations = vrsc.GetTranslations(w.WordId, 2);
				}
			}

			Random rnd = new Random();
			bookFiles = bookFiles.OrderBy(item => rnd.Next()).ToArray();

			foreach (string longFilename in bookFiles)
			{
				if (!File.Exists(longFilename)) continue;

				Console.WriteLine();
				Console.WriteLine("File: " + Path.GetFileName(longFilename));

				string[] lines = File.ReadAllLines(longFilename);

				foreach (string l in lines)
				{
					if (String.IsNullOrEmpty(l)) continue;

					foreach (string k in drills.Keys)
					{
						string[] words = l.Split(whiteSpaces);

						if (words.Contains(k))
						{
							long difficultyLevel = 0;
							foreach (string w in words)
							{
								if (w == k) continue;

								var tempWord = indexedWords.Find(w);
								if ((tempWord != null) && (tempWord.Index > difficultyLevel))
								{
									difficultyLevel = tempWord.Index;
								}

								if (difficultyLevel > drills[k].MaxDifficulty) break;
							}

							if ((difficultyLevel <= drills[k].MaxDifficulty) && (drills[k].Samples.Count(s => s.Difficulty == difficultyLevel) == 0))
							{
								string sampleSentence = l;

								string[] charsToRemove = new string[] { "“", "„", "\"", "»", "«", "›", "‹" };
								foreach (string c in charsToRemove)
								{
									sampleSentence = sampleSentence.Replace(c, "");
								}
								sampleSentence = sampleSentence.Trim();

								if ((sampleSentence[sampleSentence.Length - 1] == '.') || (sampleSentence[sampleSentence.Length - 1] == '!') || (sampleSentence[sampleSentence.Length - 1] == '?'))
								{
									drills[k].Samples.Add(new DrillSampleSentence() { Difficulty = difficultyLevel, Text = sampleSentence });

									if (drills[k].Samples.Count > maxSamplesPerWord)
									{
										// we need to remove one sample which is the furthest from the target difficulty
										DrillSampleSentence sampleToRemove = drills[k].Samples.OrderBy(s => Math.Abs(s.Difficulty - targetDifficultyLevel)).Last();
										drills[k].Samples.Remove(sampleToRemove);
									}
								}
							}
						}
					}
				}

				Console.WriteLine(String.Join("|", drills.Where(s => s.Value.Samples.Count < maxSamplesPerWord).Select(s => s.Value.Samples.Count.ToString()).ToArray()));

				if (drills.All(s => s.Value.Samples.Count >= minSamplesPerWord))
				{
					break;
				}
			}

			vrsc.Close();

			List<DrillEntry> drillEntries = drills.Values.ToList();
			drillEntries.Sort((de1, de2) => de2.Word.Frequency.Value - de1.Word.Frequency.Value);

			StreamWriter sw = File.CreateText(samplesFilename);
			foreach (DrillEntry de in drillEntries)
			{
				sw.WriteLine("--------------------------  " + String.Format("{0,-36}", de.Word.Text) + " (" + String.Format("{0,6}", de.Word.Index) + ")  --------------------------");
				de.Samples.Sort((dss1, dss2) => (int)(dss1.Difficulty - dss2.Difficulty));
				foreach (DrillSampleSentence s in de.Samples)
				{
					sw.WriteLine(String.Format("{0,6}", s.Difficulty) + "\t|" + s.Text);
				}
				if (de.Translations.Length > 0)
				{
					sw.WriteLine("- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
					foreach (Translation t in de.Translations)
					{
						sw.WriteLine(String.Join("|", new string[] { "--", t.Common, t.Uncommon, t.Rare, "--" }));
					}
				}
				sw.WriteLine("-----------------------------------------------------------------------------------------------------");
				sw.WriteLine();
				sw.WriteLine();
				sw.WriteLine();
				sw.WriteLine();
			}
			sw.Close();
		}

		private static void TranslateWords(string[] args)
		{
			int topWordCount = Int32.Parse(args[3]);

			// do this between 4'500 and 30'000'000 to cover the first 10'000 words
			int minFrequency = 4000;
			int maxFrequency = 5000;
			//
			Console.WriteLine("Getting words with frequency value between " + minFrequency.ToString("N0") + " and " + maxFrequency.ToString("N0") + " from the database...");

			VocalRecallServiceClient vrsc = new VocalRecallServiceClient();
			vrsc.Open();
			
			Culture sourceCulture = vrsc.ListCultures().FirstOrDefault(c => c.CultureIso == args[1]);
			Culture targetCulture = vrsc.ListCultures().FirstOrDefault(c => c.CultureIso == args[2]);

			int i = 0;
			Word[] wordsArray = vrsc.GetWords(sourceCulture.CultureId, minFrequency, maxFrequency);
			Console.WriteLine("We got " + wordsArray.Length.ToString("N0") + " words.");
			foreach (Word w in wordsArray)
			{
				WriteProcessFeedback(i++);
				Translation[] translations = vrsc.GetTranslations(w.WordId, targetCulture.CultureId);
			}				

			vrsc.Close();

			Console.WriteLine("Press a key to exit...");
			Console.ReadKey();

		}

		private static void ReadBooks(string[] args)
		{
			string culture = args[1];
			string[] bookFiles = Directory.GetFiles(Path.GetFullPath(args[2]), "*.txt");

			DataToUpload dtu = ProcessAllBooks(culture, bookFiles);

			Console.WriteLine("Uploading data to the database...");

			Console.WriteLine("WARNING: This process will reset all frequency and statistical data for the culture '" + culture + "'! Press a key continue...");
			Console.ReadLine();

			UploadDataToDatabase(culture, dtu);

			Console.WriteLine("Press a key to exit...");
			Console.ReadKey();
		}

		private static void UploadDataToDatabase(string culture, DataToUpload dtu)
		{
			VocalRecallServiceClient vrsc = new VocalRecallServiceClient();
			vrsc.Open();
			Culture[] cultures = vrsc.ListCultures();
			culture = culture.Replace('_', '-');
			int cultureId = cultures.Where(c => c.CultureIso == culture).FirstOrDefault().CultureId;
			List<Word> wordsToUpload = new List<Word>();
			dtu.Words.CopyAll(wordsToUpload);

			Word[] wordsArray;
			Console.WriteLine("Reseting frequency data...");
			vrsc.ResetWordFrequencies(cultureId);

			int k = 0;
			Console.WriteLine("Uploading new frequency data (" + dtu.Words.Count.ToString("N0") + ")...");
			while (dtu.Words.Count > 0)
			{
				int count = (int)Math.Min(wordsToUpload.Count, 100);
				k += 100;

				wordsArray = new Word[count];

				wordsToUpload.CopyTo(0, wordsArray, 0, count);
				wordsToUpload.RemoveRange(0, count);

				vrsc.UploadOrUpdateFrequencyForWords(cultureId, wordsArray);

				WriteProcessFeedback(k);
			}

			vrsc.UpdateCultureStatistics(cultureId, dtu.TotalBookCount, dtu.TotalWordsProcessed, dtu.TotalWordCount);
			vrsc.Close();
		}

		private static DataToUpload ProcessAllBooks(string culture, string[] bookFiles)
		{
			string uploadFilename = "DataToUpload_" + culture + ".dat";
			DataToUpload dtu = new DataToUpload();
			BinaryFormatter binaryFormatter = new BinaryFormatter();

			if (File.Exists(uploadFilename))
			{
				Console.WriteLine("Loading back data from backup... (this might take a few minutes)");

				Stream readStr = File.OpenRead(uploadFilename);
				dtu = (DataToUpload)binaryFormatter.Deserialize(readStr);
				readStr.Close();
			}
			else
			{
				IndexedWordList words = new IndexedWordList();

				foreach (string longFilename in bookFiles)
				{
					if (!File.Exists(longFilename)) continue;

					Console.WriteLine("Reading book: " + Path.GetFileName(longFilename));

					ExtractWordsFromFile(longFilename, words);

					dtu.TotalBookCount++;
				}
				Console.WriteLine("Total number of books read: " + dtu.TotalBookCount.ToString("N0"));
				Console.WriteLine();

				Console.WriteLine("Compressing words (" + words.Count.ToString("N0") + ") ...");

				//List<KeyValuePair<string, int>> wordsResorted = new List<KeyValuePair<string, int>>();
				//dtu.TotalWordsProcessed = CleanUpAndSortDictionary(wordsUnsorted, wordsResorted, 2);
				
				dtu.TotalWordsProcessed = words.TotalNumberOfWords;
				Console.WriteLine("Total number of words processed: " + dtu.TotalWordsProcessed.ToString("N0"));

				words.RemoveUnfrequentWords(2);
				Console.WriteLine();
				Console.WriteLine("Removing duplicates (" + words.Count.ToString("N0") + ") ...");

				dtu.Words = RemoveDuplicates(words);
				dtu.TotalWordCount = dtu.Words.Count;

				Console.WriteLine("Final number of words: " + dtu.TotalWordCount.ToString("N0"));

				Stream strDb = File.Create(uploadFilename);
				binaryFormatter.Serialize(strDb, dtu);
				strDb.Close();
			}

			return dtu;
		}

		private static IndexedWordList RemoveDuplicates(IndexedWordList origWords)
		{		
			int i = 0;
			List<Word> wl = new List<Word>();
			origWords.CopyAll(wl);

			IndexedWordList result = new IndexedWordList();

			foreach (Word word in wl)
			{
				WriteProcessFeedback(i++);

				Word wordInTheList = result.Find(word.Text);

				if (wordInTheList != null)
				{
					if (wordInTheList.Frequency < word.Frequency.Value)
					{
						wordInTheList.Text = word.Text;
					}
					wordInTheList.Frequency += word.Frequency.Value;
				}
				else
				{
					result.Add(word.Text, word.Frequency.Value);
				}
			}

			Console.WriteLine("done");

			return result;
		}

		private static long CleanUpAndSortDictionary(Dictionary<string, int> wordsUnsorted, List<KeyValuePair<string, int>> wordsResorted, int keepTreshold)
		{
			long totalNumberOfWordsProcessed = 0;

			List<string> removeKeys = new List<string>();
			long m = 0;
			bool smallNumberOfWords = false;

			while (wordsUnsorted.Count > 0)
			{
				WriteProcessFeedback(m++);

				if ((smallNumberOfWords) && (wordsUnsorted.First().Value > m)) continue;

				removeKeys.Clear();

				if ((!smallNumberOfWords) && (wordsUnsorted.Count <= 1000))
				{
					KeyValuePair<string, int>[] temp = wordsUnsorted.OrderBy(w => w.Value).ToArray();
					wordsUnsorted.Clear();
					foreach (KeyValuePair<string, int> item in temp)
					{
						wordsUnsorted.Add(item.Key, item.Value);
					}

					smallNumberOfWords = true;
				}

				foreach (KeyValuePair<string, int> stem in wordsUnsorted)
				{
					if (stem.Value == m)
					{
						if (m >= keepTreshold)
						{
							wordsResorted.Insert(0, stem);
						}

						removeKeys.Add(stem.Key);
						totalNumberOfWordsProcessed += m;
					}
				}

				foreach (string key in removeKeys)
				{
					wordsUnsorted.Remove(key);
				}
			}
			Console.WriteLine("..100%");

			return totalNumberOfWordsProcessed;
		}

		private static void WriteProcessFeedback(long m)
		{
			if (m < 100)
			{
				Console.Write(".");
			}
			else if ((m < 10000) && (m % 100 == 0))
			{
				Console.Write(".");
			}
			else if (m % 10000 == 0)
			{
				Console.Write(".");
			}
			if (m % 100000 == 0) Console.Write("|" + m.ToString("N0") + "|");
		}

		private static void ExtractWordsFromFile(string longFilename, IndexedWordList words)
		{
			BinaryFormatter binaryFormatter = new BinaryFormatter();

			string worddataFilename = longFilename + ".worddata";
			IndexedWordList wordData;

			if (File.Exists(worddataFilename))
			{
				Stream readStr = File.OpenRead(worddataFilename);
				wordData = (IndexedWordList)binaryFormatter.Deserialize(readStr);
				readStr.Close();
			}
			else
			{
				string fileContent = File.ReadAllText(longFilename, Encoding.UTF8);
				string[] fileWords = fileContent.Split(whiteSpaces, StringSplitOptions.RemoveEmptyEntries);

				wordData = new IndexedWordList();

				foreach (string word in fileWords)
				{
					// skip ALL CAPITAL
					if (word == word.ToUpperInvariant()) continue;

					// skip word without a vowel
					bool hasVowel = false;
					int p = 0;
					while (!hasVowel && (p < word.Length))
					{
						hasVowel |= vowels.BinarySearch(word[p]) > 0;

						p++;
					}
					if (!hasVowel) continue;

					// skip weird cases
					if (word.Split(whiteSpaces, StringSplitOptions.RemoveEmptyEntries).Length > 1) continue;

					// add it to the list
					Word w = wordData.Find(word, true);

					if (w == null)
					{
						wordData.Add(word);
					}
					else
					{
						w.Frequency++;
					}
				}

				Stream str = File.Create(worddataFilename);
				binaryFormatter.Serialize(str, wordData);
				str.Close();
			}

			words.MergeWith(wordData, true);
		}

		private static void LevelBooks(string[] args)
		{
			VocalRecallServiceClient vrsc = new VocalRecallServiceClient();
			vrsc.Open();

			Console.WriteLine("Loading word list of top 200'000 words...");
			Culture sourceCulture = vrsc.ListCultures().FirstOrDefault(c => c.CultureIso == args[1]);		
			Word[] allWords = vrsc.GetTopWords(sourceCulture.CultureId, 200000);
			
			IndexedWordList indexedWords = new IndexedWordList();
			foreach (Word w in allWords)
			{
				indexedWords.Add(w);
			}

			string[] bookFiles = Directory.GetFiles(Path.GetFullPath(args[2]), "*.txt");

			foreach (string longFilename in bookFiles)
			{
				if (!File.Exists(longFilename)) continue;

				Console.WriteLine();
				Console.WriteLine("Reading book: " + Path.GetFileName(longFilename));

				IndexedWordList words = new IndexedWordList();
				ExtractWordsFromFile(longFilename, words);

				words.RemoveUnfrequentWords(2);

				List<Word> finalWords = new List<Word>();
				RemoveDuplicates(words).CopyAll(finalWords);
				
				double totalNumberOfWordsInFile = finalWords.Sum(w => w.Frequency.Value);
				double unknownPercentage = 0.0;

				List<LevelStatEntry> stats = new List<LevelStatEntry>();
				foreach (Word fw in finalWords)
				{
					LevelStatEntry lse = new LevelStatEntry();
					lse.LocalFrequency = fw.Frequency.Value;
					lse.Percentage = (double)fw.Frequency.Value / totalNumberOfWordsInFile;
					//lse.Word = allWords.FirstOrDefault(dbw => (dbw.CultureId == sourceCulture.CultureId) && (dbw.Text.ToLower() == fw.Text.ToLower()));
					lse.Word = indexedWords.Find(fw.Text);

					if ((lse.Word != null) && (lse.Word.Frequency.HasValue))
					{
						stats.Add(lse);
					}
					else
					{
						unknownPercentage += lse.Percentage;
					}
				}

				stats.Sort((lse1, lse2) => lse2.Word.Frequency.Value - lse1.Word.Frequency.Value);

				long level95p = 0;
				StreamWriter sw = File.CreateText(longFilename + ".stats");

				double currentPercentage = unknownPercentage * 100;
				int lastReportedPercentageBorder = 80;
				foreach (LevelStatEntry se in stats)
				{
					sw.WriteLine("{0}\t{1}\t{2}\t{3}", se.LocalFrequency, (se.Percentage * 100).ToString("0.00000000"), String.Format("{0,-36}", se.Word.Text), se.Word.Index);
					currentPercentage += se.Percentage * 100;

					if (currentPercentage < 95.0)
					{
						level95p = se.Word.Index;
					}

					if (currentPercentage > lastReportedPercentageBorder)
					{
						sw.WriteLine("-------------------------------  " + currentPercentage.ToString("0") + "%  -------------------------------");
						lastReportedPercentageBorder = (int)Math.Ceiling(currentPercentage);
					}
				}
				sw.Close();

				string finalFilename = longFilename + ".lvl-" + level95p.ToString("0") + ".stats";
				if (File.Exists(finalFilename))
				{
					File.Delete(finalFilename);
				}
				File.Move(longFilename + ".stats", finalFilename);
			}
		}	
    }
}
