/****** Script for SelectTopNRows command from SSMS  ******/
SELECT TOP 100000 O.[Frequency]
      ,O.[CultureId]
      ,O.[Text]
	  ,T.[Text]
	  ,T.[CultureId]
      ,T.[Frequency]
  FROM [Suggestopedia].[dbo].[Word] AS O
  INNER JOIN [Suggestopedia].[dbo].[Translation] AS OT
  ON OT.[OriginalWordId] = O.[WordId]
  INNER JOIN [Suggestopedia].[dbo].[Word] AS T
  ON T.[WordId] = OT.[TranslationWordId]
  WHERE O.[CultureId] = 3
  AND T.[CultureId] = 2
ORDER BY O.[Frequency] DESC