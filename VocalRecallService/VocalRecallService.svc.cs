using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using VocalRecallService.DataContract;
using System.Threading;
using System.Diagnostics;

namespace VocalRecallService
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single,InstanceContextMode = InstanceContextMode.Single)]
    public class VocalRecallService : IVocalRecallService
    {
        private const int MAXIMUM_NUMBER_OF_RETURNED_ITEMS = 10;

        private List<SessionSettings> runningSessions = new List<SessionSettings>();
        private Dictionary<int, SortedDictionary<int,int>> wordIndexes = new Dictionary<int, SortedDictionary<int,int>>();

        private int GetTopXFrequency(int cultureId, int x)
        {
            int result = 0;

            if (!wordIndexes.ContainsKey(cultureId)) wordIndexes.Add(cultureId, new SortedDictionary<int,int>());

            if (wordIndexes[cultureId].Count == 0)
            {
                // rebuild indexes
                List<int> frequencies = new List<int>();


                VocalRecallEntities entities = new VocalRecallEntities();

                foreach (Word word in entities.Words.Where(w => w.CultureId == cultureId).ToArray())
                {
                    if (word.Frequency.HasValue)
                    {
                        frequencies.Add(word.Frequency.Value);
                    }
                }
                frequencies.Sort();
                frequencies.Reverse();

                int i = 0;
                while (frequencies.Count > i)
                {
                    wordIndexes[cultureId].Add(i, frequencies[i]);
                    i += MAXIMUM_NUMBER_OF_RETURNED_ITEMS;
                }
            }

            x = (x / MAXIMUM_NUMBER_OF_RETURNED_ITEMS) * MAXIMUM_NUMBER_OF_RETURNED_ITEMS;

            wordIndexes[cultureId].TryGetValue(x, out result);

            return result;
        }

        #region IVocalRecallService Members

        public void DeleteWords(int cultureId)
        {
            VocalRecallEntities entities = new VocalRecallEntities();
            // delete all words with this culture
            foreach (Word word in entities.Words.Where(w => w.CultureId == cultureId))
            {
                entities.Words.DeleteObject(word);
            }

            entities.SaveChanges(System.Data.Objects.SaveOptions.AcceptAllChangesAfterSave);

            entities.Dispose();
        }

        public int UploadWords(int cultureId, DataContract.Word[] words)
        {
            VocalRecallEntities entities = new VocalRecallEntities();

            // load up the new list
            foreach (DataContract.Word word in words)
            {
                Word newWord = entities.Words.CreateObject();
                
                newWord.CultureId = cultureId;
                newWord.Text = word.Text;
                newWord.Frequency = word.Frequency;
				newWord.FrequencyLastUpdated = DateTime.UtcNow;
                
                entities.Words.AddObject(newWord);
            }

            int result = entities.SaveChanges(System.Data.Objects.SaveOptions.AcceptAllChangesAfterSave);

            entities.Dispose();

            return result;
        }

        public DataContract.Culture[] ListCultures()
        {
            List<DataContract.Culture> result = new List<DataContract.Culture>();

            VocalRecallEntities entities = new VocalRecallEntities();
            foreach (Culture culture in entities.Cultures)
            {
                result.Add(
                    new DataContract.Culture() { 
                        CultureId = culture.CultureId, 
                        CultureIso = culture.CultureISO, 
                        Name = culture.Name 
                    });
            }
            entities.Dispose();

            return result.ToArray();
        }

        public int AuthenticateUser(string username, string password)
        {
            throw new NotImplementedException();
        }

        public DataContract.UserInfo GetUserInfo(string username, int sessionId)
        {
            throw new NotImplementedException();
        }

        public DataContract.Word GetWord(int wordId)
        {
            DataContract.Word result = new DataContract.Word();

            VocalRecallEntities entities = new VocalRecallEntities();

            Word word = entities.Words.Where(w => w.WordId == wordId).FirstOrDefault();

            if (word == null)
            {
                //result.AsyncWordResult |= AsyncWordResult.MissingEntry;
            }
            else
            {
                result.WordId = word.WordId;
                result.CultureId = word.CultureId;
                result.Text = word.Text;
                result.Frequency = word.Frequency;
                //result.Pronunciation = word.Pronunciation;
                //result.Picture = word.Picture;
                result.AlternativePictureUrls = word.AlternativePictureUrls;
                result.SelectedPictureUrl = word.SelectedPictureUrl;
				result.FrequencyLastUpdated = word.FrequencyLastUpdated;

                string language = word.Culture.CultureISO.Substring(0, 2);

                if (word.Pronunciation == null)
                {
                    //NOTE: this enum is a fucker, it causes strange communication channel exceptions between the service and client
                    //result.AsyncWordResult |= AsyncWordResult.DownloadingPronunciation;
                    
                    result.DownloadPronunciation(language);
                }

                if ((word.Picture == null) || (word.AlternativePictureUrls == null))
                {
                    //result.AsyncWordResult |= AsyncWordResult.DownloadingPicture;
                    result.DownloadPicture(language);
                }
            }

            entities.Dispose();

            return result;
        }

        public DataContract.Translation[] GetTranslations(int wordId, int cultureId)
        {
			List<DataContract.Translation> result = new List<DataContract.Translation>();

			VocalRecallEntities entities = new VocalRecallEntities();

			Word word = entities.Words.Where(w => w.WordId == wordId).FirstOrDefault();

			if (word == null)
			{
				return null;
			}
			else
			{
				DataContract.Word original = GetWord(word.WordId);

				if (word.CultureId == cultureId) return null;

				Translation[] translations = word.Translations.Where(tr => tr.CultureId == cultureId).ToArray();
				if (translations.Length == 0)
				{
					original.DownloadTranslation(original.CultureId, cultureId);
					//result.AsyncWordResult |= AsyncWordResult.DownloadingTranslation;
				}
				else
				{
					foreach (Translation t in translations)
					{
						DataContract.Translation nt = new DataContract.Translation();

						nt.TranslationId = t.TranslationId;
						nt.OriginalWordId = t.OriginalWordId;
						nt.RootWord = t.RootWord;
						nt.PartOfSpeech = t.PartsOfSpeech;
						nt.CultureId = t.CultureId;
						nt.Common = t.Common;
						nt.Uncommon = t.Uncommon;
						nt.Rare = t.Rare;
						//nt.RawResponse = t.RawResponse;
						nt.LastUpdated = t.LastUpdated;

						result.Add(nt);
					}
				}
			}

			entities.Dispose();

			return result.ToArray();
        }

        public DataContract.Word[] GetWords(int cultureId, int frequencyMinimum, int frequencyMaximum)
        {
            List<DataContract.Word> result = new List<DataContract.Word>();

            VocalRecallEntities entities = new VocalRecallEntities();

            foreach (Word word in entities.Words.Where(w => (w.CultureId == cultureId) && (w.Frequency >= frequencyMinimum) && (w.Frequency <= frequencyMaximum)).ToArray())
            {
                result.Add(GetWord(word.WordId));
            }
            
            result.Reverse();

            return result.ToArray();
        }

        public DataContract.Word[] Get10Words(int cultureId, int top)
        {
            DataContract.Word[] result = null;

            top = (((top - 1) / MAXIMUM_NUMBER_OF_RETURNED_ITEMS) + 1) * MAXIMUM_NUMBER_OF_RETURNED_ITEMS;

            int highFrequency = GetTopXFrequency(cultureId, top - MAXIMUM_NUMBER_OF_RETURNED_ITEMS);
            int lowFrequency = GetTopXFrequency(cultureId, top);

            if (lowFrequency != highFrequency) lowFrequency++;

            result = GetWords(cultureId, lowFrequency, highFrequency);

            // we have to wait until we finish downloading data
            //DateTime waitingStarted = DateTime.Now; // set a timeout for the waiting procedure to avoid deadlock
            //while ((WebDownloader.RunningThreadCount > 0))// && (DateTime.Now - waitingStarted < new TimeSpan(0, 0, 2)))
            //{
            //    Thread.Sleep(200);
            //}

            return result;
        }

        public void DeletePicture(int wordId)
        {
            VocalRecallEntities entities = new VocalRecallEntities();

            Word word = entities.Words.Where(w => w.WordId == wordId).FirstOrDefault();

            if (word != null)
            {
                word.Picture = null;
                word.SelectedPictureUrl = null;
                entities.SaveChanges(System.Data.Objects.SaveOptions.AcceptAllChangesAfterSave);
            }

            entities.Dispose();
        }

		public void ResetWordFrequencies(int cultureId)
		{
			VocalRecallEntities entities = new VocalRecallEntities();

			entities.CommandTimeout = 180;
			entities.sp_ResetFrequencies(cultureId);

			entities.Dispose();
		}

		public void UpdateCultureStatistics(int cultureId, long totalBooksRead, long totalWordsProcessed, long totalWordCount)
		{
			VocalRecallEntities entities = new VocalRecallEntities();

			Culture c = entities.Cultures.FirstOrDefault(cu => cu.CultureId == cultureId);

			c.TotalBooksRead = (int)totalBooksRead;
			c.TotalWordsProcessed = (int)totalWordsProcessed;
			c.TotalWordCount = (int)totalWordCount;

			entities.SaveChanges(System.Data.Objects.SaveOptions.AcceptAllChangesAfterSave);

			entities.Dispose();
		}

		public void UploadOrUpdateFrequencyForWords(int cultureId, DataContract.Word[] wordList)
		{
			VocalRecallEntities entities = new VocalRecallEntities();

			foreach (DataContract.Word w in wordList)
			{
				var ow = entities.sp_GetWord(cultureId, w.Text).SingleOrDefault();

				if (ow == null)
				{
					entities.sp_InsertWord(cultureId, w.Text, w.Frequency, DateTime.UtcNow);
				}
				else
				{
					entities.sp_UpdateWord(ow.WordId, w.Text, w.Frequency, DateTime.UtcNow);
				}
			}
			entities.Dispose();
		}

		public DataContract.Word[] GetTopWords(int cultureId, int topCount)
		{
			List<DataContract.Word> result = new List<DataContract.Word>();

			VocalRecallEntities entities = new VocalRecallEntities();
			var results = entities.sp_GetTopWords(cultureId, topCount);

			foreach (var r in results)
			{
				result.Add(
					new DataContract.Word()
					{
						Index = r.Index.Value,
						WordId = r.WordId,
						CultureId = r.CultureId,
						Text = r.Text,
						Frequency = r.Frequency,
						FrequencyLastUpdated = r.FrequencyLastUpdated
					}
					);
			}

			return result.ToArray();
		}

        #endregion
    }
}
