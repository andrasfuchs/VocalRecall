using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using VocalRecallService.DataContract;

namespace VocalRecallService
{
    [ServiceContract]
    public interface IVocalRecallService
    {
        [OperationContract]
        void DeleteWords(int cultureId);

        [OperationContract]
        int UploadWords(int cultureId, DataContract.Word[] words);

        [OperationContract]
        DataContract.Culture[] ListCultures();

        [OperationContract]
        int AuthenticateUser(string username, string password);

        [OperationContract]
        DataContract.UserInfo GetUserInfo(string username, int sessionId);

        [OperationContract]
        DataContract.Word GetWord(int wordId);

        [OperationContract]
        DataContract.Translation[] GetTranslations(int wordId, int cultureId);

        [OperationContract]
        DataContract.Word[] GetWords(int cultureId, int frequencyMinimum, int frequencyMaximum);

        [OperationContract]
        DataContract.Word[] Get10Words(int cultureId, int top);

        [OperationContract]
        void DeletePicture(int wordId);

		[OperationContract]
		void ResetWordFrequencies(int cultureId);

		[OperationContract]
		void UpdateCultureStatistics(int cultureId, long totalBooksRead, long totalWordsProcessed, long totalWordCount);

		[OperationContract]
		void UploadOrUpdateFrequencyForWords(int cultureId, DataContract.Word[] wordList);

		[OperationContract]
		DataContract.Word[] GetTopWords(int cultureId, int topCount);
    }
}
