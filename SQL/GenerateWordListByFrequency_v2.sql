SELECT TOP 100000 
	ROW_NUMBER() OVER( ORDER BY [Frequency] DESC ) AS [Index]
	,W.Frequency
    ,W.Text
	,Tr.RootWord
	,Tr.PartsOfSpeech
	,Tr.Common
	,Tr.Uncommon
	,Tr.Rare
  FROM [VocalRecall].[dbo].[Word] AS W
  INNER JOIN [VocalRecall].[dbo].[Translation] AS Tr
  ON [WordId] = [OriginalWordId]
  WHERE W.[CultureId] = 3
  AND [Frequency] IS NOT NULL
  AND [PartsOfSpeech] IS NOT NULL
  AND [PartsOfSpeech] <> ''
  ORDER BY [Frequency] DESC