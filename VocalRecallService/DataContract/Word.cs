using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO.Compression;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Web.Script.Serialization;

namespace VocalRecallService.DataContract
{
    public class Word
    {
        public int WordId;

        public int CultureId;

        public string Text;

        public int? Frequency;

        public byte[] Pronunciation;

        public byte[] Picture;

        public string AlternativePictureUrls;

        public string SelectedPictureUrl;

		public DateTime FrequencyLastUpdated;

		public long Index;

        public AsyncWordResult AsyncWordResult;

        public void DownloadPronunciation(string language)
        {
            //string searchString = HttpUtility.UrlEncodeUnicode(Text);
            string searchString = Text;

            string requestUrl = "http://translate.google.com/translate_tts?ie=UTF-8&q=" + searchString + "&tl=" + language;

            Debug.WriteLine("Downloading pronunciation for '" + Text + "' (" + requestUrl + ")");

            WebDownloader.QueueDownload(null, requestUrl, new WebDownloader.DownloadedDelegate(PronunciationDownloaded), null);
        }

        private void PronunciationDownloaded(byte[] data, object[] parameters)
        {
            if (data == null) return;

            Debug.WriteLine("Downloaded  pronunciation for '" + Text + "'");

            VocalRecallEntities entities = new VocalRecallEntities();
            entities.Words.Where(w => w.WordId == this.WordId).First().Pronunciation = data;
            entities.SaveChanges();
            entities.Dispose();

			Debug.WriteLine("Saved       pronunciation for '" + Text + "'");
        }

        public void DownloadPicture(string language)
        {
            if (AlternativePictureUrls == null)
            {
                string searchString = Text;

                string requestUrl = "http://www.google.com/search?tbm=isch&tbs=itp:photo,isz:m&q=" + searchString + "&hl=" + language;

				Debug.WriteLine("Searching   picture       for '" + Text + "' (" + requestUrl + ")");

                WebDownloader.QueueDownload(null, requestUrl, new WebDownloader.DownloadedDelegate(PictureDownloaded), new object[] { 0 });
            }
            else
            {
                PictureDownloaded(null, new object[] { 1 });
            }
        }

        private void PictureDownloaded(byte[] data, object[] parameters)
        {
            int state = (int)parameters[0];
            List<string> highResImageUrls = new List<string>();

            if (state == 0)
            {
                if (data == null) return;

                // build image list
                byte[] decompressedData = DecompressGzippedData(data);

                string googlePictureSearchHtml = System.Text.Encoding.UTF8.GetString(decompressedData);


                StringBuilder pictureUrls = new StringBuilder();

                int imgUrlIndex = -1;
                pictureUrls.Append('|');
                while ((imgUrlIndex = googlePictureSearchHtml.IndexOf("imgurl=")) >= 0)
                {
                    string nextUrl = WebDownloader.GetPatternMatchedInformation(googlePictureSearchHtml, "imgurl=", "&");
                    googlePictureSearchHtml = googlePictureSearchHtml.Substring(imgUrlIndex + 7);

                    if (String.IsNullOrEmpty(nextUrl)) continue;

                    highResImageUrls.Add(nextUrl);
                    pictureUrls.Append(highResImageUrls[highResImageUrls.Count - 1]);
                    pictureUrls.Append('|');
                }

                if (highResImageUrls.Count == 0)
                {
                    Debug.WriteLine("No pictures were found for '" + Text + "'");
                }
                else
                {
                    VocalRecallEntities entities = new VocalRecallEntities();
                    var word = entities.Words.Where(w => w.WordId == this.WordId).First();
                    word.AlternativePictureUrls = pictureUrls.ToString();
                    entities.SaveChanges();
                }
                state = 1;
            }

            if (state == 1)
            {
                if ((highResImageUrls.Count == 0) && (AlternativePictureUrls != null))
                {
                    foreach (string url in AlternativePictureUrls.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        highResImageUrls.Add(url);
                    }
                }

                // download the picture
                VocalRecallEntities entities = new VocalRecallEntities();
                var word = entities.Words.Where(w => w.WordId == this.WordId).First();

                if ((word.Picture == null) && (highResImageUrls.Count > 0))
                {
                    int randomIndex = new Random().Next(highResImageUrls.Count);

                    word.SelectedPictureUrl = highResImageUrls[randomIndex];
                    entities.SaveChanges();

                    string highResImageUrl = HttpUtility.UrlDecode(highResImageUrls[randomIndex]);
                    Debug.WriteLine("Downloading picture       for '" + Text + "' from '" + highResImageUrl + "'");
					                             
                    WebDownloader.QueueDownload(null, highResImageUrl, new WebDownloader.DownloadedDelegate(PictureDownloaded), new object[] { 2 });
                }

                entities.Dispose();
            }
            
            if (state == 2)
            {
                if (data == null) return;

				Debug.WriteLine("Downloaded  picture       for '" + Text + "'");

                VocalRecallEntities entities = new VocalRecallEntities();
                entities.Words.Where(w => w.WordId == this.WordId).First().Picture = data;
                entities.SaveChanges();
                entities.Dispose();

				Debug.WriteLine("Saved       picture       for '" + Text + "'");
            }
        }

        private byte[] DecompressGzippedData(byte[] data)
        {
            byte[] result = new byte[0];

            // decompress gzipped data
            MemoryStream memoryStream = new MemoryStream(data);
            GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);

            byte[] buffer = new byte[65536];
            int readBytes = 0;

            while ((readBytes = gzipStream.Read(buffer, 0, 65536)) > 0)
            {
                byte[] temp = result;
                result = new byte[temp.Length + readBytes];
                temp.CopyTo(result, 0);
                Array.Copy(buffer, 0, result, temp.Length, readBytes);
            }

            return result;
        }

        public void DownloadTranslation(int originalCultureId, int targetCultureId)
        {
            string searchString = HttpUtility.UrlEncode(Text);
            string originalLanguage = EntityCache.Cultures.Where(c => c.CultureId == originalCultureId).First().CultureISO.Substring(0, 2);
            string targetLanguage = EntityCache.Cultures.Where(c => c.CultureId == targetCultureId).First().CultureISO.Substring(0, 2);

            string requestUrl = "http://translate.google.com/translate_a/t?client=t&text=" + searchString + "&hl=en&sl=" + originalLanguage + "&tl=" + targetLanguage + "&multires=1&otf=1&ssel=0&tsel=0&sc=1";
			                             
            Debug.WriteLine("Downloading translation   for '" + Text + "' (" + requestUrl + ")");

            WebDownloader.QueueDownload(null, requestUrl, new WebDownloader.DownloadedDelegate(TranslationDownloaded), new object[] { targetCultureId });
        }

		private void TranslationDownloaded(byte[] data, object[] parameters)
		{
			if (data == null) return;

			int targetCultureId = (int)parameters[0];

			Debug.WriteLine("Downloaded  translation   for '" + Text + "'");

			VocalRecallEntities entities = new VocalRecallEntities();

			// process raw data
			byte[] decompressedData = DecompressGzippedData(data);
			string rawResponse = System.Text.Encoding.UTF8.GetString(decompressedData);

			//DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(GoogleTranslateResponse));
			//GoogleTranslateResponse gtr = (GoogleTranslateResponse)js.ReadObject(rawResponse);

			//JavaScriptSerializer js = new JavaScriptSerializer();
			//GoogleTranslateResponse gtr = js.Deserialize<GoogleTranslateResponse>(rawResponse);

			// NOTE: Unfortunatelly Google Translate return an invalid JSON like this:
			// [[["and","und","",""]],[["conjunction",["and"],[["and",["und"],,0.778800786]],"und",7],["preposition",["plus"],[["plus",["plus","zuzüglich","und"]]],"und",5]],"de",,[["and",[4],1,0,1000,0,1,0]],[["und",4,[["and",1000,1,0]],[[0,3]],"und"]],,,[],435]
			// Check it here: http://jsonlint.com/

			string[] lists = SplitJSONArray(rawResponse);

			if (!String.IsNullOrEmpty(lists[1]))
			{
				lists = SplitJSONArray(lists[1]);

				foreach (string pos in lists)
				{
					string[] posList = SplitJSONArray(pos);
					string[] meanings = SplitJSONArray(posList[2]);

					var translation = entities.Translations.CreateObject();
					translation.OriginalWordId = WordId;
					translation.CultureId = targetCultureId;
					translation.RawResponse = rawResponse;
					translation.PartsOfSpeech = posList[0];
					translation.RootWord = posList[3];
					translation.Common = "";
					translation.Uncommon = "";
					translation.Rare = "";

					foreach (string m in meanings)
					{
						string[] details = SplitJSONArray(m);

						float frequency = 0;
						if (details.Length >= 4)
						{
							Single.TryParse(details[3], out frequency);
						}

						// these limits are set by Google as of 2013-11-22
						if (frequency > 0.05f)
						{
							translation.Common += details[0] + ",";
						}
						else if (frequency > 0.003f)
						{
							translation.Uncommon += details[0] + ",";
						}
						else
						{
							translation.Rare += details[0] + ",";
						}
					}

					translation.LastUpdated = DateTime.UtcNow;
					entities.Translations.AddObject(translation);
				}
			}
			else
			{
				string[] meanings = SplitJSONArray(SplitJSONArray(SplitJSONArray(lists[5])[0])[2]);

				var translation = entities.Translations.CreateObject();
				translation.OriginalWordId = WordId;
				translation.CultureId = targetCultureId;
				translation.RawResponse = rawResponse;
				translation.PartsOfSpeech = "";
				translation.RootWord = "";
				
				foreach (string m in meanings)
				{
					translation.Common += SplitJSONArray(m)[0] + ",";
				}

				translation.Uncommon = "";
				translation.Rare = "";

				translation.LastUpdated = DateTime.UtcNow;
				entities.Translations.AddObject(translation);
			}

			entities.SaveChanges();
			Debug.WriteLine("Saved       translation   for '" + Text + "'");

			entities.Dispose();
		}

		private string[] SplitJSONArray(string jsonArray)
		{
			List<string> result = new List<string>();

			if ((jsonArray[0] != '[') || (jsonArray[jsonArray.Length - 1] != ']'))
			{
				throw new Exception("JSON string '" + jsonArray + "' is not an array!");
			}

			int brackets = 0;
			int lastComma = 1;

			for (int i=1; i<jsonArray.Length-1; i++)
			{
				if (jsonArray[i] == '[') brackets++;
				if (jsonArray[i] == ']') brackets--;
				if ((jsonArray[i] == ',') && (brackets == 0))
				{
					result.Add(jsonArray.Substring(lastComma, i - lastComma));
					lastComma = i + 1;
				}
			}

			result.Add(jsonArray.Substring(lastComma, jsonArray.Length - 1 - lastComma));

			for (int j=0; j<result.Count; j++)
			{
				if (String.IsNullOrEmpty(result[j])) continue;

				if (result[j][0] == '\"') result[j] = result[j].Substring(1);

				if ((result[j].Length > 0) && (result[j][result[j].Length-1] == '\"')) result[j] = result[j].Substring(0, result[j].Length - 1);
			}

			return result.ToArray();
		}

    }

    public enum AsyncWordResult { AllOk = 0, DownloadingPronunciation = 1, DownloadingPicture = 2, DownloadingTranslation = 4, MissingEntry = 8, Error = 16 }
}