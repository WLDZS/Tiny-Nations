// Amplify Shader Editor - Visual Shader Editing Tool
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AmplifyShaderEditor
{
	[System.Serializable]
	public class Toast
	{
		public MessageSeverity ItemType;
		public string ItemMessage;
		public double ItemTime;
		public Toast( MessageSeverity itemType, string itemMessage, double itemTime )
		{
			ItemType = itemType;
			ItemMessage = itemMessage;
			ItemTime = itemTime;
		}
	}

	public class ConsoleLogWindow
	{
		public const int MAXWIDTH = 400;
		public const float FADETIME = 7;

		private readonly GUIContent m_boxToggleContent = new GUIContent( "\u2261", "Toggle Message Box" );
		private readonly GUIContent m_clearContent = new GUIContent( "\u00D7", "Clear Messages" );

		protected AmplifyShaderEditorWindow m_parentWindow = null;
		
		// needs to be serialized
		private Vector2 m_currentScrollPos;

		int lastCall = -1;

		public ConsoleLogWindow( AmplifyShaderEditorWindow parentWindow )
		{
			m_parentWindow = parentWindow;
		}

		public void AddMessage( MessageSeverity itemType, string itemMessage )
		{
			var toast = new Toast( itemType, itemMessage, Time.realtimeSinceStartup );
			m_parentWindow.Messages.Insert( 0, toast );
			m_currentScrollPos.y = Mathf.Infinity;
			
			if( !m_parentWindow.MaximizeMessages )
				lastCall = Mathf.Max( (int)itemType, lastCall );

			GUIContent gc = new GUIContent( m_parentWindow.Messages.Count + ": " + itemMessage );
			float maxWidth = m_parentWindow.MaxMsgWidth;
			maxWidth = Mathf.Max( GUIStyle.none.CalcSize( gc ).x + 16, maxWidth );
			maxWidth = Mathf.Min( maxWidth, MAXWIDTH );
			m_parentWindow.MaxMsgWidth = maxWidth;
		}

		public void Draw( Rect parentPosition, Vector2 mousePosition, int mouseButtonId, bool hasKeyboadFocus, float rightSide )
		{
			EventType currentEventType = Event.current.type;
			var messages = m_parentWindow.Messages;
			var maximize = m_parentWindow.MaximizeMessages;
			
			Rect button = parentPosition;
			button.width = 22;
			button.height = 22;
			button.x = parentPosition.x + parentPosition.width - button.width - rightSide - 8;
			button.y = parentPosition.y + parentPosition.height - button.height - ( m_parentWindow.CurrentSelection == ASESelectionMode.Material ? 52 : 8 );

			Rect toolbarArea = button;
			toolbarArea.y -= 5;
			if( maximize )
			{
				toolbarArea.xMin -= m_parentWindow.MaxMsgWidth;
				toolbarArea.yMin -= 66;
			}
			toolbarArea.x -= 6;

			bool needsRepaint = false;
			var cached = GUI.color;
			if( maximize )
			{
				toolbarArea.y -= 16 + 8;
				GUILayout.BeginArea( toolbarArea, UIUtils.ConsoleLogMessage );
				EditorGUILayout.BeginVertical();
				m_currentScrollPos = EditorGUILayout.BeginScrollView( m_currentScrollPos );
				{
					int count = messages.Count;
					for( int i = count - 1; i >= 0; i-- )
					{
						switch( messages[ i ].ItemType )
						{
							case MessageSeverity.Error:
							GUI.color = Color.red;
							break;
							case MessageSeverity.Warning:
							GUI.color = Color.yellow;
							break;
							default:
							case MessageSeverity.Normal:
							GUI.color = cached;
							break;
						}
						GUILayout.Label( (count - i) + ": " + messages[ i ].ItemMessage );
					}
				}
				EditorGUILayout.EndScrollView();
				EditorGUILayout.EndVertical();
				
				GUILayout.EndArea();
			} 
			else
			{
				// draw toaster
				int count = messages.Count;
				Rect rect = toolbarArea;
				rect.xMin -= 200;
				
				float startFade = FADETIME - 1;
				for( int i = 0; i < count; i++ )
				{
					float delta = (float)(Time.realtimeSinceStartup - messages[ i ].ItemTime);
					if( delta > FADETIME )
						continue;

					if( delta < 0.1f )
					{
						GUI.color = Color.cyan;
					}
					else if( delta < startFade )
					{
						switch( messages[ i ].ItemType )
						{
							case MessageSeverity.Error:
							GUI.color = Color.red;
							break;
							case MessageSeverity.Warning:
							GUI.color = Color.yellow;
							break;
							default:
							case MessageSeverity.Normal:
							GUI.color = Color.white;
							break;
						}
					}
					else
					{
						switch( messages[ i ].ItemType )
						{
							case MessageSeverity.Error:
							GUI.color = new Color( 1, 0, 0, FADETIME - delta );
							break;
							case MessageSeverity.Warning:
							GUI.color = new Color( 1, 1, 0, FADETIME - delta );
							break;
							default:
							case MessageSeverity.Normal:
							GUI.color = new Color( 1, 1, 1, FADETIME - delta );
							break;
						}
					}

					needsRepaint = true;

					GUIContent gc = new GUIContent( messages[ i ].ItemMessage );
					GUIStyle msgstyle = UIUtils.ConsoleLogMessage;
					var sizes = msgstyle.CalcSize( gc );
					rect.xMin -= sizes.x - rect.width;
					rect.height = sizes.y;
					rect.y -= rect.height + 2;
					GUI.Label( rect, gc, msgstyle );
				}
			}
			GUI.color = cached;
			
			if( needsRepaint )
				m_parentWindow.MarkToRepaint();

			GUIStyle style = UIUtils.ConsoleLogCircle;

			button.size = Vector2.one * 16;
			
			switch( lastCall )
			{
				case 0: 
				style.normal.textColor = Color.cyan;
				break;
				case 1:
				style.normal.textColor = Color.yellow;
				break;
				case 2:
				style.normal.textColor = Color.red;
				break;
				default:
				style.normal.textColor = new Color( 1, 1, 1, 0.5f );
				break;
			}
			
			if( GUI.Button( button, m_boxToggleContent, style ) )
			{
				maximize = !maximize;
				m_parentWindow.MaximizeMessages = maximize;
				m_currentScrollPos.y = Mathf.Infinity;
				lastCall = -1;
			}

			style.normal.textColor = new Color( 1, 1, 1, 0.5f );
			GUI.color = cached;
			button.x -= button.width + 2;

			if( maximize && GUI.Button( button, m_clearContent, style ) )
			{
				if( messages.Count == 0 )
				{
					maximize = false;
					m_parentWindow.MaximizeMessages = maximize;
				}
				ClearMessages();
			}

			button.width += button.width + 2;
			bool mouseOnTop = button.Contains( mousePosition );

			if( currentEventType == EventType.MouseMove && mouseOnTop )
				m_parentWindow.MarkToRepaint();

			if( DebugConsoleWindow.DeveloperMode )
			{
				if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha1 )
				{
					UIUtils.ShowMessage( "This is an info message\nwith two lines" );
				}

				if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha2 )
				{
					UIUtils.ShowMessage( "This is a warning message", MessageSeverity.Warning );
				}

				if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha3 )
				{
				
					UIUtils.ShowMessage( "THIS IS AN ERROR MESSAGE!!", MessageSeverity.Error );
				}
			}
		}

		public void ClearMessages()
		{
			m_parentWindow.Messages.Clear();
			m_parentWindow.MaxMsgWidth = 100;
		}

		public void Toggle()
		{
			
		}

		public void Destroy()
		{
			m_parentWindow = null;
		}
	}
}
