using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_dump : Part
	{
		// settings
		[KSPField]
		public float fuelFlow;
		[KSPField]
		public float rcsFlow;

		const int windowId = 1763;

		// ...
		static Part guiOwner = null;
		Rect windowPositionAndSize = new Rect();
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		const int windowWidth = 100;

		bool fuelDumpFlag = false;
		bool RCSDumpFlag = false;

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

			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Fuel", (fuelDumpFlag) ? (activeStyle) : (style), GUILayout.ExpandWidth (true))) {
				fuelDumpFlag = !fuelDumpFlag;
			}
			if (GUILayout.Button ("RCS", (RCSDumpFlag) ? (activeStyle) : (style), GUILayout.ExpandWidth (true))) {
				RCSDumpFlag = !RCSDumpFlag;
			}
			GUILayout.EndHorizontal ();

			GUILayout.EndVertical();

			// dragable window
			GUI.DragWindow();
		}

		private void drawGUI ()
		{
			if ((vessel == FlightGlobals.ActiveVessel) && isControllable) {
				if (windowPositionInvalid) {
					windowPositionInvalid = false;
					windowPositionAndSize.x = 10;
					windowPositionAndSize.y = 60;
				}
				if(windowSizeInvalid) {
					windowSizeInvalid = false;
					windowPositionAndSize.width = 10;
					windowPositionAndSize.height = 10;
				}
				GUI.skin = HighLogic.Skin;
				windowPositionAndSize = GUILayout.Window (windowId, windowPositionAndSize, windowGUI, "Fuel Dump", GUILayout.MinWidth (windowWidth));	 
			}
		}

		// init
		protected override void onPartLoad ()
		{
			base.onPartLoad();
		}

		// editor + flight
		protected override void onPartAwake ()
		{
			base.onPartAwake();
		}

		protected override void onPartStart ()
		{
			base.onPartStart();
			stackIcon.SetIcon(DefaultIcons.MYSTERY_PART);

			if (fuelFlow <= 0)
				fuelFlow = 100;
			if (rcsFlow <= 0)
				rcsFlow = 25;

			print ("ORDA_dump cfg-settings: " + fuelFlow.ToString("F3") + " " + rcsFlow.ToString("F3"));
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy ();
		}

		// flight
		protected override void onFlightStart ()
		{
			base.onFlightStart ();
		}

		protected override void onPartUpdate ()
		{
			base.onPartUpdate();

			if (FlightGlobals.ActiveVessel == this.vessel) {
				// register gui
				if(guiOwner == null) {
					guiOwner = this;
					RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));
				}
			} else {
				// release gui
				if(guiOwner == this) {
					guiOwner = null;
					RenderingManager.RemoveFromPostDrawQueue (0, new Callback (drawGUI));
				}
			}
		}

		protected override void onPartFixedUpdate ()
		{
			base.onPartFixedUpdate ();

			float dt = Time.fixedDeltaTime;

			// fuel dump logic
			if (fuelDumpFlag) {
				float amount = Mathf.Abs (fuelFlow) * dt;
				RequestFuel (this, amount, getFuelReqId ());
			}
			if (RCSDumpFlag) {
				float amount = Mathf.Abs (rcsFlow) * dt;
				vessel.rootPart.RequestRCS (amount, 0);
			}
		}
	}
}
