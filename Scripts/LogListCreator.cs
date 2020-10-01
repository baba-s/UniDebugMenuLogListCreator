using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kogane.DebugMenu
{
	public sealed class LogListTabData
	{
		public string Text       { get; }
		public string SearchText { get; }

		public LogListTabData
		(
			string text,
			string searchText
		)
		{
			Text       = text;
			SearchText = searchText;
		}
	}

	/// <summary>
	/// ログ情報のリストを作成するクラス
	/// </summary>
	[Serializable]
	public sealed class LogListCreator : ListCreatorBase<ActionData>, IDisposable
	{
		//==============================================================================
		// 列挙型
		//==============================================================================
		private enum TabType
		{
			ALL,       // 全て
			LOG,       // 情報
			WARNING,   // 警告
			ERROR,     // エラー
			EXCEPTION, // 例外
			LENGTH,
		}

		//==============================================================================
		// クラス
		//==============================================================================
		/// <summary>
		/// 出力されたログの情報を管理するクラス
		/// </summary>
		private sealed class LogData
		{
			//==========================================================================
			// 変数(readonly)
			//==========================================================================
			public TabType Type          { get; } // タブの種類
			public string  FullCondition { get; } // 概要（全部）
			public string  Message       { get; } // 表示用のテキスト

			private readonly string m_stackTrace; // スタックトレース
			private readonly string m_dateTime;   // 日時

			//==========================================================================
			// 関数
			//==========================================================================
			// コンストラクタ
			public LogData
			(
				TabType type,
				string  fullCondition,
				string  condition,
				string  stackTrace,
				string  dateTime
			)
			{
				Type          = type;
				FullCondition = fullCondition;
				m_stackTrace  = stackTrace;
				m_dateTime    = dateTime;

				var colorTag = ToColorTag( type );

				Message = $"<color={colorTag}>[{dateTime}] {condition}</color>";
			}

			// コンストラクタ
			public LogData
			(
				string  fullCondition,
				string  condition,
				string  stackTrace,
				LogType type
			) : this
			(
				type: ToTabType( type ),
				fullCondition: fullCondition,
				condition: condition,
				stackTrace: stackTrace,
				dateTime: DateTime.Now.ToString( "HH:mm:ss" )
			)
			{
			}

			// 一行分のログ情報に変換して返します
			public IEnumerable<LogData> ToSimpleLogList( int maxCharacterCountPerLine )
			{
				var conditions = FullCondition
						.Split( '\n' )
						.SelectMany( x => SubstringAtCount( x, maxCharacterCountPerLine ) )
					;

				foreach ( var n in conditions )
				{
					var logData = new LogData
					(
						type: Type,
						fullCondition: FullCondition,
						condition: n,
						stackTrace: m_stackTrace,
						dateTime: m_dateTime
					);

					yield return logData;
				}
			}

			// 詳細な文字列に整形して返します
			public override string ToString()
			{
				var builder = new StringBuilder();

				builder.AppendLine( "<b>DateTime</b>" );
				builder.AppendLine( "　" );
				builder.AppendLine( m_dateTime );
				builder.AppendLine( "　" );
				builder.AppendLine( "<b>Condition</b>" );
				builder.AppendLine( "　" );
				builder.AppendLine( FullCondition );
				builder.AppendLine( "　" );
				builder.AppendLine( "<b>StackTrace</b>" );
				builder.AppendLine( "　" );
				builder.AppendLine( m_stackTrace );
				builder.AppendLine( "　" );

				return builder.ToString();
			}

			// ログの種類をタブの種類に変換します
			private static TabType ToTabType( LogType self )
			{
				switch ( self )
				{
					case LogType.Log:       return TabType.LOG;
					case LogType.Warning:   return TabType.WARNING;
					case LogType.Error:     return TabType.ERROR;
					case LogType.Assert:    return TabType.ERROR;
					case LogType.Exception: return TabType.EXCEPTION;
				}

				return TabType.ALL;
			}

			// タブの種類を color タグに変換します
			private static string ToColorTag( TabType self )
			{
				switch ( self )
				{
					case TabType.WARNING:   return "#ffff00";
					case TabType.ERROR:     return "#ff0000";
					case TabType.EXCEPTION: return "#ff00ff";
				}

				return "#ffffff";
			}
		}

		//==============================================================================
		// 変数(static)
		//==============================================================================
		private static readonly List<LogData> m_logList = new List<LogData>(); // ログ情報のリスト

		private static readonly string[] DEFAULT_TAG_NAME_LIST =
		{
			"全て",
			"情報",
			"警告",
			"エラー",
			"例外",
		};

		//==============================================================================
		// 変数
		//==============================================================================
		private ActionData[] m_list;

		//==============================================================================
		// プロパティ
		//==============================================================================
		public override int Count => m_list.Length;

		public override string[] TabNameList =>
			ExtraTagDatas != null
				? DEFAULT_TAG_NAME_LIST.Concat( ExtraTagDatas.Select( x => x.Text ) ).ToArray()
				: DEFAULT_TAG_NAME_LIST;

		public override ActionData[] OptionActionList =>
			new[]
			{
				new ActionData( "クリア", () => Clear() ),
			};

		public int              MaxLogCount              { get; set; } = 1000;
		public int              MaxCharacterCountPerLine { get; set; } = 120;
		public LogListTabData[] ExtraTagDatas            { get; set; }

		//==============================================================================
		// 関数
		//==============================================================================
		/// <summary>
		/// コンストラクタ
		/// </summary>
		public LogListCreator()
		{
			Application.logMessageReceivedThreaded += HandleLog;
		}

		/// <summary>
		/// イベントを解除します
		/// </summary>
		public void Dispose()
		{
			Application.logMessageReceivedThreaded -= HandleLog;

			m_logList.Clear();
			m_list = null;
		}

		/// <summary>
		/// ログが出力された時に呼び出されます
		/// </summary>
		private void HandleLog
		(
			string  condition,
			string  stackTrace,
			LogType type
		)
		{
			var data = new LogData( condition, condition, stackTrace, type );
			m_logList.Insert( 0, data );

			while ( MaxLogCount <= m_logList.Count )
			{
				m_logList.RemoveAt( m_logList.Count - 1 );
			}
		}

		/// <summary>
		/// リストの表示に使用するデータを作成します
		/// </summary>
		protected override void DoCreate( ListCreateData data )
		{
			var tabIndex            = data.TabIndex;
			var tabType             = ( TabType ) tabIndex;
			var isAll               = tabType == TabType.ALL;
			var extraTagData        = ExtraTagDatas?.ElementAtOrDefault( tabIndex - ( int ) TabType.LENGTH );
			var isExistExtraTagData = extraTagData != null;
			var extraTagSearchText  = isExistExtraTagData ? extraTagData.SearchText : string.Empty;

			m_list = m_logList
					.Where( x => isAll || x.Type == tabType || isExistExtraTagData && x.FullCondition.Contains( extraTagSearchText ) )
					.SelectMany( x => x.ToSimpleLogList( MaxCharacterCountPerLine ) )
					.Where( x => data.IsMatch( x.FullCondition ) )
					.Select( x => new ActionData( x.Message, () => OpenAdd( DMType.TEXT_TAB_6, new SimpleInfoCreator( x.ToString(), MaxCharacterCountPerLine ) ) ) )
					.ToArray()
				;

			if ( data.IsReverse )
			{
				Array.Reverse( m_list );
			}
		}

		/// <summary>
		/// 指定されたインデックスの要素の表示に使用するデータを返します
		/// </summary>
		protected override ActionData DoGetElemData( int index )
		{
			return m_list.ElementAtOrDefault( index );
		}

		/// <summary>
		/// ログをすべて消去します
		/// </summary>
		private void Clear()
		{
			m_logList.Clear();
			UpdateDisp();
		}

		/// <summary>
		/// 指定された文字列を指定された文字数で分割して返します
		/// </summary>
		private static string[] SubstringAtCount( string self, int count )
		{
			var result = new List<string>();
			var length = ( int ) Math.Ceiling( ( double ) self.Length / count );

			for ( int i = 0; i < length; i++ )
			{
				int start = count * i;

				if ( self.Length <= start )
				{
					break;
				}

				if ( self.Length < start + count )
				{
					result.Add( self.Substring( start ) );
				}
				else
				{
					result.Add( self.Substring( start, count ) );
				}
			}

			return result.ToArray();
		}
	}
}