using System;
using System.Collections.Generic;
using System.Text;
using LSLib.LS;
using LaughingLocale.Data.History;
using LaughingLocale.Data.Locale;
using LaughingLocale.ViewModel;
using LaughingLocale.ViewModel.Locale;

namespace LaughingLocale.Divinity.Data
{
	public class DivinityLocaleEntry : BaseHistoryData, ILocaleData
	{
		public ILocaleContainer Parent { get; set; }

		public bool ChangesUncommitted { get; set; } = false;

		public virtual void CommitChanges()
		{
			ChangesUncommitted = false;
		}

		private string key;

		public string Key
		{
			get => key;
			set
			{
				SetKey(value, false, true);
			}
		}

		private void SetKey(string value, bool skipUpdatingSource = false, bool withHistory = false)
		{
			if (KeyAttribute != null)
			{
				if (skipUpdatingSource || !Equals(KeyAttribute.Value, value))
				{
					if(withHistory)
					{
						var undoVal = key;
						var redoVal = value;

						void undo()
						{
							key = undoVal;
							if (keyAttribute != null) keyAttribute.Value = key;
							Notify("Key");
						};
						void redo()
						{
							key = redoVal;
							if (keyAttribute != null) keyAttribute.Value = key;
							Notify("Key");
						}

						UpdateWithHistory(ref key, value, undo, redo);
					}
					else
					{
						Update(ref key, value);
					}

					if(!skipUpdatingSource)
					{
						keyAttribute.Value = key;
					}
				}
			}
			else
			{
				Update(ref key, value);
			}
		}

		private string content;

		public string Content
		{
			get => content;
			set
			{
				SetContent(value, false, true);
			}
		}

		private void SetContent(string value, bool skipUpdatingSource = false, bool withHistory = false)
		{
			if (translatedString != null)
			{
				if (skipUpdatingSource || !Equals(translatedString.Value, value))
				{
					if(withHistory)
					{
						var undoVal = handle;
						var redoVal = value;

						void undo()
						{
							content = undoVal;
							if (translatedString != null) translatedString.Value = content;
							Notify("Content");
						};
						void redo()
						{
							content = redoVal;
							if (translatedString != null) translatedString.Value = content;
							Notify("Content");
						};

						UpdateWithHistory(ref content, value, undo, redo);
					}
					else
					{
						Update(ref content, value);
					}

					if(!skipUpdatingSource)
					{
						translatedString.Value = content;
					}
				}
			}
			else
			{
				Update(ref content, value);
			}
		}

		private string handle = "ls::TranslatedStringRepository::s_HandleUnknown";

		public string Handle
		{
			get => handle;
			set
			{
				SetHandle(value, false, true);
			}
		}

		private void SetHandle(string value, bool skipUpdatingSource = false, bool withHistory = false)
		{
			if (translatedString != null)
			{
				if (skipUpdatingSource || !Equals(translatedString.Handle, value))
				{
					if(withHistory)
					{
						var undoVal = handle;
						var redoVal = value;

						void undo()
						{
							handle = undoVal;
							if (translatedString != null) translatedString.Handle = handle;
							Notify("Handle");
						};
						void redo()
						{
							handle = redoVal;
							if (translatedString != null) translatedString.Handle = handle;
							Notify("Handle");
						};

						UpdateWithHistory(ref handle, value, undo, redo);
					}
					else
					{
						Update(ref handle, value);
					}

					if (!skipUpdatingSource) translatedString.Handle = handle;
				}
			}
			else
			{
				Update(ref handle, value);
			}
		}

		private bool locked = false;

		public bool Locked
		{
			get => locked;
			set
			{
				Update(ref locked, value);
			}
		}

		private bool selected = false;

		public bool Selected
		{
			get => selected;
			set
			{
				Update(ref selected, value);
			}
		}

		#region LSLib
		private Node sourceNode;

		public Node SourceNode
		{
			get { return sourceNode; }
			set { sourceNode = value; }
		}

		private NodeAttribute keyAttribute;

		public NodeAttribute KeyAttribute
		{
			get { return keyAttribute; }
			set
			{
				keyAttribute = value;
				if (keyAttribute != null)
				{
					SetKey((string)keyAttribute.Value, true, false);
				}
			}
		}

		private TranslatedString translatedString;

		public TranslatedString TranslatedString
		{
			get { return translatedString; }
			set
			{
				translatedString = value;
				if(translatedString != null)
				{
					SetContent(translatedString.Value, true, false);
					SetHandle(translatedString.Handle, true, false);
				}
			}
		}

		private bool _initialized = false;

		public HistorySnapshotAction Load(Node node, NodeAttribute ka, TranslatedString ts)
		{
			var lastKey = Key;
			var lastContent = Content;
			var lastHandle = Handle;

			var redoKey = ka != null ? (string)ka.Value : "";
			var redoContent = ts != null ? (string)ts.Value : "";
			var redoHandle = ts != null ? (string)ts.Handle : "";

			void undo()
			{
				Key = lastKey;
				Content = lastContent;
				Handle = lastHandle;
			};

			void redo()
			{
				Key = redoKey;
				Content = redoContent;
				Handle = redoHandle;
			}

			SourceNode = node;
			KeyAttribute = ka;
			TranslatedString = ts;

			return new HistorySnapshotAction()
			{
				Undo = undo,
				Redo = redo
			};
		}
		#endregion
	}
}
