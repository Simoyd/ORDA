using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_decoupler : Decoupler
	{
		// settings
		const int windowId = 1762;
		const float highlightDelay = 2.0f;
		const float safetyTimerDelay = 2.0f;
		const float springForce = 10.0f;

		// ...
		Rect windowPositionAndSize = new Rect();
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		const int windowWidth = 200;
		ORDA_decoupler highlightedDecoupler = null;
		float highlightTimer = 0;

		static List<ORDA_decoupler> decouplerList = new List<ORDA_decoupler>();
		static ORDA_decoupler guiInstance = null;
		static int decouplerListItems = -1;

		bool settingsValid = false;
		string decouplerName = "Decoupler";
		string decouplerKey = "1";

		bool keyPressFlag = false;
		bool decoupleSafety = false;
		float decoupleSafetyTimer = 0;
		bool decoupledFlag = false;
		bool springFlag = false;

		bool doNotActivate = true;

		// gui
		private void windowGUI (int windowID)
		{
			GUIStyle style = new GUIStyle (GUI.skin.button); 
			style.normal.textColor = style.focused.textColor = Color.white;
			style.hover.textColor = style.active.textColor = Color.yellow;
			style.onNormal.textColor = style.onFocused.textColor = style.onHover.textColor = style.onActive.textColor = Color.green;
			style.padding = new RectOffset (4, 4, 4, 4);

			GUIStyle activeStyle = new GUIStyle (GUI.skin.button); 
			activeStyle.normal.textColor = activeStyle.focused.textColor = Color.red;
			activeStyle.hover.textColor = activeStyle.active.textColor = Color.yellow;
			activeStyle.onNormal.textColor = activeStyle.onFocused.textColor = activeStyle.onHover.textColor = activeStyle.onActive.textColor = Color.green;
			activeStyle.padding = new RectOffset (4, 4, 4, 4);

			GUILayout.BeginVertical ();

			foreach (ORDA_decoupler dcpl in decouplerList) {

				GUILayout.BeginHorizontal();

				bool changed = false;
				string before;

				// name
				before = dcpl.decouplerName;
				dcpl.decouplerName = GUILayout.TextField(dcpl.decouplerName, GUILayout.MaxWidth(100.0f));
				if(dcpl.decouplerName != before) {
					changed = true;
				}

				// key
				before = dcpl.decouplerKey;
				dcpl.decouplerKey = GUILayout.TextField(dcpl.decouplerKey, GUILayout.MaxWidth(25.0f));
				if(dcpl.decouplerKey.Length > 1 || dcpl.decouplerKey.Length < 1) {
					dcpl.decouplerKey = "1";
				}
				if(dcpl.decouplerKey != before) {
					changed = true;
				}

				// highlight?
				if(GUILayout.Button ("show")) {
					// reset old highlight
					if(highlightedDecoupler != null) {
						highlightedDecoupler.highlight(ColorHighlightNone);
						highlightedDecoupler = null;
					}

					//foreach(ORDA_decoupler dcpl2 in decouplerList) {
					//	dcpl2.highlight(ColorHighlightNone);
					//}
					// highlight this decoupler
					dcpl.highlight(Color.green);
					highlightedDecoupler = dcpl;
					highlightTimer = 0;
				}

				GUILayout.EndHorizontal();

				// save it to customPartData
				if(changed) {
					dcpl.onBackup();
				}
			}

			GUILayout.EndVertical();

			// dragable window
			GUI.DragWindow();
		}

		private void drawGUI ()
		{
			if (decouplerListItems != decouplerList.Count) {
				decouplerListItems = decouplerList.Count;
				windowSizeInvalid = true;
			}
			if (windowPositionInvalid) {
				windowPositionInvalid = false;
				windowPositionAndSize.x = Screen.width - windowWidth - 100;
				windowPositionAndSize.y = 50;
			}
			if(windowSizeInvalid) {
				windowSizeInvalid = false;
				windowPositionAndSize.width = 10;
				windowPositionAndSize.height = 10;
			}
			GUI.skin = HighLogic.Skin;
			windowPositionAndSize = GUILayout.Window (windowId, windowPositionAndSize, windowGUI, "Decoupler", GUILayout.MinWidth (windowWidth));	 
		}

		//
		// init
		//
		protected override void onPartLoad ()
		{
			base.onPartLoad();
		}

		//
		// editor + flight
		//
		protected override void onPartAwake ()
		{
			base.onPartAwake();
		}

		protected override void onPartStart ()
		{
			base.onPartStart ();

			decouplerList.Add (this);
			stackIcon.SetIcon (DefaultIcons.MYSTERY_PART);

			// get settings from custromPartData
			if (customPartData.Length > 0) {
				ORDAdecouplersettings settings = (ORDAdecouplersettings)KSP.IO.IOUtils.DeserializeFromBinary (Convert.FromBase64String (customPartData.Replace ("*", "=").Replace ("|", "/")));
				decouplerName = settings.name;
				decouplerKey = settings.key;
			}
			settingsValid = true;
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy ();

			// remove gui if we created it
			if (guiInstance == this) {
				RenderingManager.RemoveFromPostDrawQueue (0, new Callback (drawGUI));
				guiInstance = null;
			}

			decouplerList.Remove(this);
		}

		public override void onBackup ()
		{
			base.onBackup ();

			// only after onPartStart has been called
			if (settingsValid) {

				// store settings in customPartData
				ORDAdecouplersettings settings = new ORDAdecouplersettings ();
				settings.name = decouplerName;
				settings.key = decouplerKey;
				customPartData = Convert.ToBase64String (KSP.IO.IOUtils.SerializeToBinary (settings)).Replace ("=", "*").Replace ("/", "|");
			}
		}

		protected override void onEditorUpdate ()
		{
			base.onEditorUpdate();
			float dt = Time.fixedDeltaTime;

			// create gui if there is none
			if (guiInstance == null) {
				RenderingManager.AddToPostDrawQueue (0, new Callback (drawGUI));
				guiInstance = this;
			}

			// reset highlight?
			if(highlightedDecoupler != null) {
				highlightTimer += dt;
				if(highlightTimer > highlightDelay) {
					highlightedDecoupler.highlight(ColorHighlightNone);
					highlightedDecoupler = null;
					highlightTimer = 0;
				}
			}
		}

		//
		// flight
		//
		protected override void onFlightStart ()
		{
			base.onFlightStart ();
		}

		protected override void onPartUpdate ()
		{
			base.onPartUpdate ();

			// skip?
			if(FlightGlobals.ActiveVessel != vessel) return;
			if(decoupledFlag) return;

			// seems to get ignored sometimes in onPartFixedUpdate()
			if (Input.GetKeyDown (decouplerKey)) {
				keyPressFlag = true;
			}
		}

		protected override void onPartFixedUpdate ()
		{
			base.onPartFixedUpdate ();

			// apply spring simple force? (todo: apply force to both parts)
			if (springFlag) {
				springFlag = false;
				rigidbody.AddRelativeForce (new Vector3 (0, -springForce, 0));
			}

			// skip?
			if(FlightGlobals.ActiveVessel != vessel) return;
			if(decoupledFlag) return;

			// key pressed?
			if (keyPressFlag) {
				keyPressFlag = false;

				// tap twice to decouple
				if (decoupleSafety == true) {

					// get all strut connectors
					List<StrutConnector> strutConnectorList = new List<StrutConnector>();
					foreach(Part p in vessel.parts) {
						if(p is StrutConnector) {
							strutConnectorList.Add((StrutConnector)p);
						}
					}

					// decouple
					decoupledFlag = true;
					doNotActivate = false;
					this.force_activate();
					this.highlight(ColorHighlightNone);

					/* todo: remove all strut connectors from 
					 * the list that should stay on our vessel,
					 * but that doesn't work for some reason
					 * */

					// kill all strut connectors no longer part of our vessel
					foreach(StrutConnector c in strutConnectorList) {
						c.Die();
					}
				
					// push away from decoupled part on next tick
					springFlag = true;

				} else {
					decoupleSafety = true;
					this.highlight (Color.green);
				}
			}

			// return to safe state after some time
			if (decoupleSafety) {
				decoupleSafetyTimer += Time.deltaTime;
				if(decoupleSafetyTimer > safetyTimerDelay) {
					decoupleSafetyTimer = 0;
					decoupleSafety = false;
					this.highlight(ColorHighlightNone);
				}
			}
		}

		// ...
		protected override bool onPartActivate ()
		{
			if (doNotActivate) {
				return false;
			}

			return base.onPartActivate();
		}
	}

	[Serializable()]
	public class ORDAdecouplersettings
	{
		public string name = "";
		public string key = "";
	}
};
