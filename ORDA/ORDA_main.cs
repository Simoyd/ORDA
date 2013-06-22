using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ORDA;

namespace ORDA
{
	public class ORDA_main : Part
	{
		// settings
		const int windowId = 1761;

		// unit objects
		bool flightStarted = false;
		VisualHelper visualHelper = null;
		FlightData flightData = null;
		Propulsion propulsion = null;
		GNC gnc = null;
		Bus bus = null;
		Teleporter teleporter = null;

		// for unit activation, in case there is more than one
		Vessel myVessel = null;
		int myVesselParts = 0;
		bool activeSystem = false;
        bool wasControllable = false;

		// gui
		enum PageType { PAGE_TARGET=0, PAGE_ORBIT, PAGE_RENDEZVOUS, PAGE_LANDING };
		PageType currentPage = PageType.PAGE_TARGET;
		bool windowIsMinimized = true;
		Rect windowPositionAndSize;
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		const int fullWindowWidth = 350;
		const int minimizedWindowWidth = 100;

		// gui target page
		Vector2 targetScrollVector = new Vector2();
		Part highlightedVesselDockingPort = null;
		Part highlightedTargetDockingPort = null;

		// gui orbit page
		bool predictTimeToggle = false;
		enum PredictMode { SHIP_AP=0, SHIP_PE, TGT_AP, TGT_PE };
		PredictMode predictMode = PredictMode.SHIP_AP;

		// gui rendezvous & docking page
		bool statToggle = false;
		bool relVectorsToggle = false;
		bool referenceToggle = false;
        bool wooble = false;
		string Kp_AngVel_string = null;
		string Kp_AngAcc_string = null;
		string Kp_Vel_string = null;
		string Kp_Acc_string = null;
		string eacPulseLength_string = null;
		string eacPulseLevel_string = null;
		string eacRate_string = null;

		// gui landing page
		bool landingStatsToggle = false;
		bool landingImpactToggle = false;

		// target
		bool targetChanged = false;
		Vessel targetVessel = null;
		Part vesselDockingPort = null;
		Part targetDockingPort = null;

		// cheats
		const bool enableCheats = true;

        bool? lastFrameFly = null;

        bool protectionOn = false;
        int protectionStart = 0;

		//
		// fly by wire handler
		//
        private void fly(FlightCtrlState s)
		{
            try
            {
                if (!activeSystem)
                    return;

                bus.yawReq = bus.pitchReq = bus.rollReq = 0;

                if ((this.vessel == null) || (this.vessel.packed) || (!isControllable))
                {
                    return;
                }

                if (lastFrameFly.HasValue && lastFrameFly.Value)
                {
                    //print("*********ORDA on " + getNameString() + " frame count desync (fly by wire)");
                }
                else
                {
                    if (!lastFrameFly.HasValue)
                    {
                        protectionStart = System.Environment.TickCount;
                        if (!protectionOn)
                        {
                            protectionOn = true;
                        }

                        //print("*********ORDA on " + getNameString() + " frame count started on fly by wire");

                        lastFrameFly = true;

                        // We havnt updated the bus yet, so just get out
                        return;
                    }
                    else
                    {
                        lastFrameFly = true;
                    }
                }

                if (protectionOn)
                {
                    if ((this.vessel.geeForce <= 0.1) || ((System.Environment.TickCount - protectionStart) > 10000))
                    {
                        protectionOn = false;
                    }
                }

                bus.yawReq = Mathf.Clamp(s.yaw, -1.0f, +1.0f);
                bus.pitchReq = Mathf.Clamp(s.pitch, -1.0f, +1.0f);
                bus.rollReq = Mathf.Clamp(s.roll, -1.0f, +1.0f);

                if (bus.yprDriven)
                {
                    if (bus.yprRelative)
                    {
                        s.yaw = Mathf.Clamp(s.yaw + bus.yaw, -1.0f, +1.0f);
                        s.pitch = Mathf.Clamp(s.pitch + bus.pitch, -1.0f, +1.0f);
                        s.roll = Mathf.Clamp(s.roll + bus.roll, -1.0f, +1.0f);
                    }
                    else
                    {
                        s.yaw = Mathf.Clamp(bus.yaw, -1.0f, +1.0f);
                        s.pitch = Mathf.Clamp(bus.pitch, -1.0f, +1.0f);
                        s.roll = Mathf.Clamp(bus.roll, -1.0f, +1.0f);
                    }
                }

                if (bus.xyzDriven)
                {
                    if (bus.xyzRelative)
                    {
                        s.X = Mathf.Clamp(s.X + bus.x, -1.0f, +1.0f);
                        s.Y = Mathf.Clamp(s.Y + bus.z, -1.0f, +1.0f);
                        s.Z = Mathf.Clamp(s.Z + bus.y, -1.0f, +1.0f);
                    }
                    else
                    {
                        s.X = Mathf.Clamp(bus.x, -1.0f, +1.0f);
                        s.Y = Mathf.Clamp(bus.z, -1.0f, +1.0f);
                        s.Z = Mathf.Clamp(bus.y, -1.0f, +1.0f);
                    }
                }
            }
            catch
            {
            }
		}

        #region GUI

        private void windowGUI (int windowID)
		{
			PageType oldPage = currentPage;

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

			if (windowIsMinimized) {
				if (GUILayout.Button ("Off", activeStyle)) {
					windowIsMinimized = false;
					windowPositionInvalid = true;
					windowSizeInvalid = true;
				}
			} else {
				GUILayout.BeginVertical ();

				// page selector
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Off", (style))) {
					windowIsMinimized = true;
					windowPositionInvalid = true;
					windowSizeInvalid = true;
				}
				if (GUILayout.Button ("Tgt", (currentPage == PageType.PAGE_TARGET) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_TARGET;
				}
				if (GUILayout.Button ("Orbit", (currentPage == PageType.PAGE_ORBIT) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_ORBIT;
				}
				if (GUILayout.Button ("Rendezvous & Dock", (currentPage == PageType.PAGE_RENDEZVOUS) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_RENDEZVOUS;
				}
				if (GUILayout.Button ("Land", (currentPage == PageType.PAGE_LANDING) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_LANDING;
				}
				GUILayout.EndHorizontal ();

				// page content
				switch (currentPage) {
				case PageType.PAGE_TARGET:
					windowTargetGUI (style, activeStyle);
					break;
				case PageType.PAGE_ORBIT:
					windowOrbitGUI (style, activeStyle);
					break;
				case PageType.PAGE_RENDEZVOUS:
					windowRendezvousGUI (style, activeStyle);
					break;
				case PageType.PAGE_LANDING:
					windowLandingGUI (style, activeStyle);
					break;
				default:
					break;
				}

				GUILayout.EndVertical ();
			}

			// dragable window
			GUI.DragWindow ();

			// resize window if page changed
			if (oldPage != currentPage) {
				windowSizeInvalid = true;
			}

			// highlight docking ports
			if (currentPage == PageType.PAGE_TARGET) {
				// remove old highlights
				if(highlightedVesselDockingPort != vesselDockingPort && highlightedVesselDockingPort != null) {
                    highlightedVesselDockingPort.SetHighlight(false);
				}
				if(highlightedTargetDockingPort != targetDockingPort && highlightedTargetDockingPort != null) {
					highlightedTargetDockingPort.SetHighlight(false);
				}

				// highlight selected ports
				if(vesselDockingPort != null) {
                    vesselDockingPort.SetHighlight(true);
				}
				if(targetDockingPort != null) {
                    targetDockingPort.SetHighlight(true);
				}
				highlightedVesselDockingPort = vesselDockingPort;
				highlightedTargetDockingPort = targetDockingPort;

			} else {
				// remove highlights
				if(highlightedVesselDockingPort != null) {
                    highlightedVesselDockingPort.SetHighlight(false);
					highlightedVesselDockingPort = null;
				}
				if(highlightedTargetDockingPort != null) {
                    highlightedTargetDockingPort.SetHighlight(false);
					highlightedTargetDockingPort = null;
				}
			}
		}

		private void windowTargetGUI (GUIStyle style, GUIStyle activeStyle)
		{
			int num;

			targetChanged = false;

			// select own docking port
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Vessel port: ");
			if (GUILayout.Button ("-", (vesselDockingPort == null) ? (activeStyle) : (style))) {
				vesselDockingPort = null;
			}
			num = 1;
			foreach (Part p in vessel.parts) {
                if (ORDA_dock.isDockable(p))
                {
					if (GUILayout.Button (num.ToString (), (vesselDockingPort == p) ? (activeStyle) : (style))) {
						vesselDockingPort = p;
					}
					num++;
				}
			}
			GUILayout.EndHorizontal ();

			// select target vessel
			GUILayout.Label ("Available Targets:");
			targetScrollVector = GUILayout.BeginScrollView (targetScrollVector, GUILayout.Height (250));
			if (GUILayout.Button ("-", (targetVessel == null) ? (activeStyle) : (style))) {
				targetVessel = null;
				targetDockingPort = null;
				targetChanged = true;
			}

			List<Vessel> vesselList = Util.getSortedVesselList(this.vessel);
			foreach (Vessel v in vesselList) {
				if (v == this.vessel)
					continue;
				if (v.Landed)
					continue;

				double distance = (this.vessel.orbit.pos - v.orbit.pos).magnitude;

				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("[" + v.vesselName + "] (" + Util.formatValue (distance, "m", "F3") + ")", (targetVessel == v) ? (activeStyle) : (style))) {
					targetVessel = v;
					targetDockingPort = null;
					targetChanged = true;
				}
				GUILayout.EndHorizontal ();

				// select target docking port
				if (v == targetVessel) {
					GUILayout.BeginHorizontal ();
					GUILayout.Label ("Target Port: ");
					if (GUILayout.Button ("-", (targetDockingPort == null) ? (activeStyle) : (style))) {
						targetDockingPort = null;
						targetChanged = true;
					}
					num = 1;
					foreach (Part p in v.parts) {
						if(ORDA_dock.isDockable(p)) {
							if (GUILayout.Button (num.ToString (), (targetDockingPort == p) ? (activeStyle) : (style))) {
								targetDockingPort = p;
								targetChanged = true;
							}
							num++;
						}
					}
					GUILayout.EndHorizontal ();
				}
			}
			GUILayout.EndScrollView ();

			// teleporter
			if (enableCheats) {
				if (targetVessel != null) {
					if (GUILayout.Button ("Teleport to Target")) {
						print ("BeamMeUpScotty");
						teleporter.beamMeUpScotty();
					}
				}
			}

			// resize window?
			if (targetChanged) {
				windowSizeInvalid = true;
			}
		}

		private void windowOrbitGUI (GUIStyle style, GUIStyle activeStyle)
		{
			bool oldPredictTimeToggle = predictTimeToggle;

			GUILayout.BeginHorizontal ();

			GUILayout.BeginVertical ();
			GUILayout.Label ("ApA");
			GUILayout.Label ("PeA");
			GUILayout.Label ("Altitude");
			GUILayout.Label ("Time to ApA");
			GUILayout.Label ("Time to PeA");
			GUILayout.Label ("Inclination");
			GUILayout.Label ("LAN");
			GUILayout.Label ("AoP");
			GUILayout.EndVertical ();

			GUILayout.BeginVertical ();
			GUILayout.Label (Util.formatValue (vessel.orbit.ApA, "m", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.PeA, "m", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.altitude, "m", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.timeToAp, "s"));
			GUILayout.Label (Util.formatValue (vessel.orbit.timeToPe, "s"));
			GUILayout.Label (Util.formatValue (vessel.orbit.inclination, "°", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.LAN, "°", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.argumentOfPeriapsis, "°", "F3"));
			GUILayout.EndVertical ();

			if (targetVessel != null) {
				GUILayout.BeginVertical ();
				GUILayout.Label (Util.formatValue (targetVessel.orbit.ApA, "m", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.PeA, "m", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.altitude, "m", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.timeToAp, "s"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.timeToPe, "s"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.inclination, "°", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.LAN, "°", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.argumentOfPeriapsis, "°", "F3"));
				GUILayout.EndVertical ();
			}

			GUILayout.EndHorizontal ();

			if (targetVessel != null) {

				// target info
				GUILayout.Label ("Target: '" + targetVessel.vesselName + "' (" + Util.formatValue(flightData.targetRelPosition.magnitude, "m", "F3") + ")");

				// Time to ApA/PeA prediction
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Predict Time to", (predictTimeToggle) ? (activeStyle) : (style))) {
					predictTimeToggle = !predictTimeToggle;
				}
				string predictString;
				switch(predictMode) {
				case PredictMode.SHIP_AP:	predictString = "Ship ApA"; break;
				case PredictMode.SHIP_PE:	predictString = "Ship PeA"; break;
				case PredictMode.TGT_AP:	predictString = "Target ApA"; break;
				case PredictMode.TGT_PE:	predictString = "Target PeA"; break;
				default:					predictString = "?"; break;
				}
				if (GUILayout.Button (predictString, style)) {
					if(predictMode == PredictMode.TGT_PE) {
						predictMode = PredictMode.SHIP_AP;
					} else {
						predictMode++;
					}
				}
				GUILayout.EndHorizontal ();

				if(predictTimeToggle) {

					bool predictShip = (predictMode == PredictMode.SHIP_AP || predictMode == PredictMode.SHIP_PE)?(true):(false);
					bool predictApA = (predictMode == PredictMode.SHIP_AP || predictMode == PredictMode.TGT_AP)?(true):(false);

					// get time to interception
					double ti = 0;
					if(predictShip) {
						ti = Util.getInterceptTime(vessel, targetVessel, predictApA);
					} else {
						ti = Util.getInterceptTime(targetVessel, vessel, predictApA);
					}

					// get times to first contact
					double vesselTimeToFirst = 0;
					double targetTimeToFirst = 0;
					if(predictMode == PredictMode.SHIP_AP || predictMode == PredictMode.SHIP_PE) {
						vesselTimeToFirst = (predictApA)?(vessel.orbit.timeToAp):(vessel.orbit.timeToPe);
						targetTimeToFirst = ti;
					} else {
						vesselTimeToFirst = ti;
						targetTimeToFirst = (predictApA)?(targetVessel.orbit.timeToAp):(targetVessel.orbit.timeToPe);
					}
					double vesselPeriod = vessel.orbit.period;
					double targetPeriod = targetVessel.orbit.period;

					// look into future
					const int numPredictions = 10;
					double[] vesselTimes = new double[numPredictions];
					double[] targetTimes = new double[numPredictions];
					int i, u;

					for (i=0; i<numPredictions; i++) {
						vesselTimes [i] = vesselTimeToFirst + i * vesselPeriod;
						targetTimes [i] = targetTimeToFirst + i * targetPeriod;
					}

					// get closest encounter
					double closestDelta = 0;
					int closestVesselIndex = -1;
					int closestTargetIndex = -1;
					for (i=0; i<numPredictions; i++) {
						for (u=0; u<numPredictions; u++) {
							double delta = vesselTimes [i] - targetTimes [u];
							if (delta < 0)
								delta *= -1;

							if (delta < closestDelta || closestVesselIndex == -1) {
								closestDelta = delta;
								closestVesselIndex = i;
								closestTargetIndex = u;
							}
						}
					}

					// visualization
					GUIStyle activeTextStyle = new GUIStyle (GUI.skin.button);
					activeStyle.normal.textColor = activeStyle.focused.textColor = Color.red;
					activeStyle.padding = new RectOffset (4, 4, 4, 4);

					GUILayout.BeginHorizontal ();

					GUILayout.BeginVertical ();
					GUILayout.Label ("#");
					for (i=0; i<numPredictions; i++) {
						GUILayout.Label (i.ToString ());
					}
					GUILayout.EndVertical ();
					GUILayout.BeginVertical ();
					GUILayout.Label ("Vessel");
					for (i=0; i<numPredictions; i++) {
						if (closestVesselIndex == i) {
							GUILayout.Label (Util.formatValue (vesselTimes [i], "s"), activeTextStyle);
						} else {
							GUILayout.Label (Util.formatValue (vesselTimes [i], "s"));
						}
					}
					GUILayout.EndVertical ();
					GUILayout.BeginVertical ();
					GUILayout.Label ("Target");
					for (i=0; i<numPredictions; i++) {
						if (closestTargetIndex == i) {
							GUILayout.Label (Util.formatValue (targetTimes [i], "s"), activeTextStyle);
						} else {
							GUILayout.Label (Util.formatValue (targetTimes [i], "s"));
						}
					}
					GUILayout.EndVertical ();

					GUILayout.EndHorizontal ();

					GUILayout.Label ("MinDelta: " + Util.formatValue (closestDelta, "s", "F3"));
				}
			}

			// resize window if content changed
			if (predictTimeToggle != oldPredictTimeToggle) {
				windowSizeInvalid = true;
			}
		}

		private void windowRendezvousGUI (GUIStyle style, GUIStyle activeStyle)
		{
			bool oldStatToggle = statToggle;

			// get some styles
			GUIStyle snormal = new GUIStyle ();
			GUIStyle sgreen = new GUIStyle ();
			GUIStyle sred = new GUIStyle ();
			sgreen.normal.textColor = Color.green;
			sred.normal.textColor = Color.red;

			// get gnc states
			GNC.Command gncCommand;
			GNC.RateMode gncRateMode;
            GNC.AttMode gncAttMode;
            GNC.UpMode gncUpMode;
            GNC.EACMode gncEacMode;
			GNC.PosMode gncPosMode;
			GNC.DockMode gncDockMode;
			gnc.getStates (out gncCommand, out gncRateMode, out gncAttMode, out gncUpMode, out gncEacMode, out gncPosMode, out gncDockMode);

			// command
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Command: ");
			if (GUILayout.Button ("OFF", (gncCommand == GNC.Command.OFF) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.OFF);
				windowSizeInvalid = true;
			}
			if (GUILayout.Button ("RATE", (gncCommand == GNC.Command.RATE) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.RATE);
				windowSizeInvalid = true;
			}
			if (GUILayout.Button ("ATT", (gncCommand == GNC.Command.ATT) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.ATT);
				windowSizeInvalid = true;
			}
			if (GUILayout.Button ("EAC", (gncCommand == GNC.Command.EAC) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.EAC);
				windowSizeInvalid = true;
			}
            if (GUILayout.Button("DOCK", (gncCommand == GNC.Command.DOCK) ? (activeStyle) : (style)))
            {
                gnc.requestCommand(GNC.Command.DOCK);
                windowSizeInvalid = true;
            }
            if (GUILayout.Button("AWES", (gncCommand == GNC.Command.AWES) ? (activeStyle) : (style)))
            {
                gnc.requestCommand(GNC.Command.AWES);
                windowSizeInvalid = true;
            }
            GUILayout.EndHorizontal();

			// rate
			if (gncCommand == GNC.Command.RATE) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Rate: ");
				if (GUILayout.Button ("ZERO", (gncRateMode == GNC.RateMode.ZERO) ? (activeStyle) : (style))) {
					gnc.requestRateMode (GNC.RateMode.ZERO);
				}
				if (GUILayout.Button ("ROLL", (gncRateMode == GNC.RateMode.ROLL) ? (activeStyle) : (style))) {
					gnc.requestRateMode (GNC.RateMode.ROLL);
				}
				if (GUILayout.Button ("HOLD", (gncRateMode == GNC.RateMode.HOLD) ? (activeStyle) : (style))) {
					gnc.requestRateMode (GNC.RateMode.HOLD);
				}
				GUILayout.EndHorizontal ();
			}
			// att
			else if (gncCommand == GNC.Command.ATT) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Att: ");
				if (GUILayout.Button ("Universe Reference", (gncAttMode == GNC.AttMode.REF) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.REF);
				}
				if (GUILayout.Button ("Hold Current", (gncAttMode == GNC.AttMode.HOLD) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.HOLD);
				}
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Vel+", (gncAttMode == GNC.AttMode.VP) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.VP);
                }
                if (GUILayout.Button("Vel-", (gncAttMode == GNC.AttMode.VN) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.VN);
                }
                if (GUILayout.Button("Sky", (gncAttMode == GNC.AttMode.RP) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RP);
                }
                if (GUILayout.Button("Ground", (gncAttMode == GNC.AttMode.RN) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RN);
                }
                if (GUILayout.Button("North", (gncAttMode == GNC.AttMode.NP) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.NP);
                }
                if (GUILayout.Button("South", (gncAttMode == GNC.AttMode.NN) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.NN);
                }
                if (GUILayout.Button("NODE", (gncAttMode == GNC.AttMode.NODE) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.NODE);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target: ");
                if (GUILayout.Button("Dir+", (gncAttMode == GNC.AttMode.RPP) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RPP);
                }
                if (GUILayout.Button("Dir-", (gncAttMode == GNC.AttMode.RPN) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RPN);
                }
                if (GUILayout.Button("Vel+", (gncAttMode == GNC.AttMode.RVP) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RVP);
                }
                if (GUILayout.Button("Vel-", (gncAttMode == GNC.AttMode.RVN) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RVN);
                }
                if (GUILayout.Button("Adj+", (gncAttMode == GNC.AttMode.RCP) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RCP);
                }
                if (GUILayout.Button("Adj-", (gncAttMode == GNC.AttMode.RCN) ? (activeStyle) : (style)))
                {
                    gnc.requestAttMode(GNC.AttMode.RCN);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Up: ");
                if (GUILayout.Button("Sky", (gncUpMode == GNC.UpMode.RP) ? (activeStyle) : (style)))
                {
                    gnc.requestUpMode(GNC.UpMode.RP);
                }
                if (GUILayout.Button("Ground", (gncUpMode == GNC.UpMode.RN) ? (activeStyle) : (style)))
                {
                    gnc.requestUpMode(GNC.UpMode.RN);
                }
                if (GUILayout.Button("North", (gncUpMode == GNC.UpMode.NP) ? (activeStyle) : (style)))
                {
                    gnc.requestUpMode(GNC.UpMode.NP);
                }
                if (GUILayout.Button("South", (gncUpMode == GNC.UpMode.NN) ? (activeStyle) : (style)))
                {
                    gnc.requestUpMode(GNC.UpMode.NN);
                }
                GUILayout.EndHorizontal();
            }
			// eac
			else if (gncCommand == GNC.Command.EAC) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Mode: ");
				if (GUILayout.Button ("PULSE", (gncEacMode == GNC.EACMode.PULSE) ? (activeStyle) : (style))) {
					gnc.requestEacMode (GNC.EACMode.PULSE);
				}
				if (GUILayout.Button ("RATE", (gncEacMode == GNC.EACMode.RATE) ? (activeStyle) : (style))) {
					gnc.requestEacMode (GNC.EACMode.RATE);
				}
				GUILayout.EndHorizontal ();
			}
			// dock
			else if (gncCommand == GNC.Command.DOCK) {

				GNC.DockState gncDockState;
				GNC.DockAbort gncDockAbort;
				gnc.getDockState (out gncDockState, out gncDockAbort);

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Mode: ");
				if (GUILayout.Button ("ATTITUDE", (gncDockMode == GNC.DockMode.ATTITUDE) ? (activeStyle) : (style))) {
					gnc.requestDockMode (GNC.DockMode.ATTITUDE);
					windowSizeInvalid = true;
				}
				if (GUILayout.Button ("AUTO", (gncDockMode == GNC.DockMode.AUTO) ? (activeStyle) : (style))) {
					gnc.requestDockMode (GNC.DockMode.AUTO);
					windowSizeInvalid = true;
				}
				GUILayout.EndHorizontal ();

				if (gncDockMode == GNC.DockMode.AUTO) {
					if (GUILayout.Button ((gncDockState == GNC.DockState.IDLE) ? ("Engage") : ("Reset"), style)) {
						gnc.requestDockEngage ();
					}
					GUILayout.BeginHorizontal ();
					GUILayout.Label ("IDLE", (gncDockState == GNC.DockState.IDLE) ? (sgreen) : (snormal));
					GUILayout.Label ("ENTRY", (gncDockState == GNC.DockState.ENTRY) ? (sgreen) : (snormal));
					GUILayout.Label ("ORIENT", (gncDockState == GNC.DockState.ORIENT) ? (sgreen) : (snormal));
					GUILayout.Label ("APPROACH", (gncDockState == GNC.DockState.APPROACH) ? (sgreen) : (snormal));
					GUILayout.Label ("DOCKED", (gncDockState == GNC.DockState.DOCKED) ? (sgreen) : (snormal));
					GUILayout.Label ("DEPART", (gncDockState == GNC.DockState.DEPART) ? (sgreen) : (snormal));
					GUILayout.EndHorizontal ();
					if (gncDockState == GNC.DockState.ABORT) {
						string abortReason = "unknown";
						switch (gncDockAbort) {
						case GNC.DockAbort.DEVIATION:
							abortReason = "Approach deviation > " + Util.formatValue (GNC.dockAbortDeviation, "°");
							break;
						case GNC.DockAbort.ATTITUDE:
							abortReason = "Attitude error > " + Util.formatValue (GNC.dockAbortAttitude, "°");
							break;
						case GNC.DockAbort.LATCH:
							abortReason = "No latch indication";
							break;
						}
						GUILayout.Label ("ABORT: " + abortReason, sred);
					}
				}

				ORDA_dock dock = (ORDA_dock)vesselDockingPort;
				Vector3 relPos;
				float distance;
				Vector3 euler;
				dock.getRelPosAndAtt (targetDockingPort, out relPos, out distance, out euler);

				GUILayout.Label ("Rel. Att. [°]: " + euler.ToString ("F3"));
				GUILayout.Label ("Rel. Pos. [m]: " + relPos.ToString ("F3"));
				GUILayout.Label ("Rel. Vel. [m/s]: " + flightData.targetRelVelocityShip.ToString ("F3"));
                GUILayout.Label("Distance: " + Util.formatValue(relPos.magnitude, "m", "F3"));
                GUILayout.Label("Approach Speed: " + Util.formatValue(flightData.targetRelVelocityShip.magnitude, "m/s"));
				GUILayout.Label ("Approach Deviation [°]: " + gnc.dockDeviationAngle.ToString ("F3"));
			}

            else if (gncCommand == GNC.Command.AWES)
            {
                // [CW]TODO: implement UI here
            }

			// position
			if (gncCommand != GNC.Command.DOCK && gncCommand != GNC.Command.AWES && targetVessel != null) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Position: ");
				if (GUILayout.Button ("ZERO", (gncPosMode == GNC.PosMode.ZERO) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.ZERO);
				}
				if (GUILayout.Button ("HOLD", (gncPosMode == GNC.PosMode.HOLD) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.HOLD);
				}
				if (GUILayout.Button ("VN", (gncPosMode == GNC.PosMode.VN) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.VN);
				}
				if (GUILayout.Button ("RN", (gncPosMode == GNC.PosMode.RN) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.RN);
				}
				if (GUILayout.Button ("RETREAT", (gncPosMode == GNC.PosMode.RETREAT) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.RETREAT);
				}
				GUILayout.EndHorizontal ();

				GUILayout.Label ("Target: '" + targetVessel.vesselName + "'");
                GUILayout.Label("Distance: " + Util.formatValue(flightData.targetRelPosition.magnitude, "m", "F3"));
                GUILayout.Label("Velocity: " + Util.formatValue(flightData.targetRelVelocityShip.magnitude, "m/s"));
                GUILayout.Label("Rel. Pos. [m]: " + flightData.targetRelPositionShip.ToString("F3"));
				GUILayout.Label ("Rel. Vel. [m/s]: " + flightData.targetRelVelocityShip.ToString ("F3"));
			}

            GUILayout.BeginHorizontal();
            GUILayout.Label("Wooble: ");
            string temp = GUILayout.TextField(flightData.woobleFactor.ToString("0.0000"), GUILayout.MaxWidth(100.0f));
            try
            {
                flightData.woobleFactor = float.Parse(temp);
            }
            catch
            {
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Debug: " + flightData.debugValue);

			// toggles
			GUILayout.BeginHorizontal ();
			statToggle = GUILayout.Toggle (statToggle, "Stats & Sett", GUILayout.ExpandWidth (true));
			relVectorsToggle = GUILayout.Toggle (relVectorsToggle, "Rel VnP", GUILayout.ExpandWidth (true));
            referenceToggle = GUILayout.Toggle(referenceToggle, "Ref", GUILayout.ExpandWidth(true));
            wooble = GUILayout.Toggle(wooble, "woob", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

			// stats
			if (statToggle) {
                GUILayout.Label("geeForce: " + this.vessel.geeForce.ToString("F3"));
                GUILayout.Label("angularVelocity: " + flightData.angularVelocity.ToString("F3"));
				GUILayout.Label ("attError: " + gnc.attError.ToString ("F3"));
				GUILayout.Label ("avelError: " + gnc.avelError.ToString ("F3"));
				GUILayout.Label ("rposError: " + gnc.rposError.ToString ("F3"));
				GUILayout.Label ("rvelError: " + gnc.rvelError.ToString ("F3"));
				if (flightData.targetVessel != null) {
					GUILayout.Label ("relPosShip: " + flightData.targetRelPositionShip.ToString ("F3"));
					GUILayout.Label ("relVelShip: " + flightData.targetRelVelocityShip.ToString ("F3"));
				}
				//GUILayout.Label ("mass: " + Util.formatValue(flightData.mass, "t"));
				//GUILayout.Label ("MoI: " + flightData.MoI.ToString("F3"));
				GUILayout.Label ("availableAngAcc: " + flightData.availableAngAcc.ToString("F3"));
				GUILayout.Label ("availableLinAcc: " + flightData.availableLinAcc.ToString("F3"));

				// controller settings
				float Kp_AngVel = 0;
				float Kp_AngAcc = 0;
				float Kp_Vel = 0;
				float Kp_Acc = 0;
				gnc.getControllerSettings (out Kp_AngVel, out Kp_AngAcc, out Kp_Vel, out Kp_Acc);

				if (Kp_AngVel_string == null) {
					Kp_AngVel_string = Kp_AngVel.ToString ("F3");
					Kp_AngAcc_string = Kp_AngAcc.ToString ("F3");
					Kp_Vel_string = Kp_Vel.ToString ("F3");
					Kp_Acc_string = Kp_Acc.ToString ("F3");
				}

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_AngVel: " + Kp_AngVel.ToString ("F3"), GUILayout.Width(fullWindowWidth/2));
				Kp_AngVel_string = GUILayout.TextField (Kp_AngVel_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_AngAcc: " + Kp_AngAcc.ToString ("F3"), GUILayout.Width(fullWindowWidth/2));
				Kp_AngAcc_string = GUILayout.TextField (Kp_AngAcc_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_Vel: " + Kp_Vel.ToString ("F3"), GUILayout.Width(fullWindowWidth/2));
				Kp_Vel_string = GUILayout.TextField (Kp_Vel_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_Acc: " + Kp_Acc.ToString ("F3"), GUILayout.Width(fullWindowWidth/2));
				Kp_Acc_string = GUILayout.TextField (Kp_Acc_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Update", style)) {
					double d = 0;
					if (Double.TryParse (Kp_AngVel_string, out d))
						Kp_AngVel = (float)d;
					if (Double.TryParse (Kp_AngAcc_string, out d))
						Kp_AngAcc = (float)d;
					if (Double.TryParse (Kp_Vel_string, out d))
						Kp_Vel = (float)d;
					if (Double.TryParse (Kp_Acc_string, out d))
						Kp_Acc = (float)d;
					gnc.setControllerSettings (Kp_AngVel, Kp_AngAcc, Kp_Vel, Kp_Acc);
				}
				if (GUILayout.Button ("Reset", style)) {
					Kp_AngVel = GNC.Default_Kp_AngVel;
					Kp_AngAcc = GNC.Default_Kp_AngAcc;
					Kp_Vel = GNC.Default_Kp_Vel;
					Kp_Acc = GNC.Default_Kp_Acc;
					gnc.setControllerSettings (Kp_AngVel, Kp_AngAcc, Kp_Vel, Kp_Acc);
					Kp_AngVel_string = null;
				}
				GUILayout.EndHorizontal ();

				// eac settings
				if(gncCommand == GNC.Command.EAC) {
					float eacPulseLength = 0;
					float eacPulseLevel = 0;
					float eacRate = 0;
					gnc.getEACSettings(out eacPulseLength, out eacPulseLevel, out eacRate);

					if(eacPulseLength_string == null) {
						eacPulseLength_string = eacPulseLength.ToString("F3");
						eacPulseLevel_string = eacPulseLevel.ToString("F3");
						eacRate_string = eacRate.ToString("F3");
					}

					GUILayout.BeginHorizontal();
					GUILayout.Label ("eacPulseLength: " + eacPulseLength.ToString("F3"), GUILayout.Width(fullWindowWidth/2));
					eacPulseLength_string = GUILayout.TextField (eacPulseLength_string, GUILayout.ExpandWidth(true));
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
					GUILayout.Label ("eacPulseLevel: " + eacPulseLevel.ToString("F3"), GUILayout.Width(fullWindowWidth/2));
					eacPulseLevel_string = GUILayout.TextField (eacPulseLevel_string, GUILayout.ExpandWidth(true));
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
					GUILayout.Label ("eacRate: " + eacRate.ToString("F3"), GUILayout.Width(fullWindowWidth/2));
					eacRate_string = GUILayout.TextField (eacRate_string, GUILayout.ExpandWidth(true));
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal();
					if (GUILayout.Button ("Update", style)) {
						double d = 0;
						if(Double.TryParse(eacPulseLength_string, out d))
							eacPulseLength = (float)d;
						if(Double.TryParse(eacPulseLevel_string, out d))
							eacPulseLevel = (float)d;
						if(Double.TryParse(eacRate_string, out d))
							eacRate = (float)d;
						gnc.setEACSettings(eacPulseLength, eacPulseLevel, eacRate);
					}
					if (GUILayout.Button ("Reset", style)) {
						eacPulseLength = GNC.Default_eacPulseLength;
						eacPulseLevel = GNC.Default_eacPulseLevel;
						eacRate = GNC.Default_eacRate;
						gnc.setEACSettings(eacPulseLength, eacPulseLevel, eacRate);
						eacPulseLength_string = null;
					}
					GUILayout.EndHorizontal();
				}

				// dock settings
				if(gncCommand == GNC.Command.DOCK) {
					bool gncInvertUp = false;
					gnc.getDockSettings(out gncInvertUp);

					GUILayout.BeginHorizontal();
					GUILayout.Label ("Invert up vector: " + gncInvertUp.ToString(), GUILayout.Width(fullWindowWidth/2));
					if(GUILayout.Button ("Toggle", style)) {
						gncInvertUp = !gncInvertUp;
						gnc.setDockSettings(gncInvertUp);
					}
					GUILayout.EndHorizontal();
				}
			}

			// resize window?
			if (oldStatToggle != statToggle) {
				windowSizeInvalid = true;
			}
		}

		private void windowLandingGUI (GUIStyle style, GUIStyle activeStyle)
		{
			bool oldStatToggle = landingStatsToggle;
			bool oldImpactToggle = landingImpactToggle;

			// visualization
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("AGL: " + Util.formatValue (flightData.altitudeAGL, "m"), GUILayout.Width(fullWindowWidth/3));
			GUILayout.Label ("VS: " + Util.formatValue (flightData.verticalSpeed, "m/s"));
			GUILayout.Label ("HS: " + Util.formatValue (flightData.horizontalSpeed, "m/s"));
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			landingStatsToggle = GUILayout.Toggle (landingStatsToggle, "Show stats", GUILayout.ExpandWidth (true));
			landingImpactToggle = GUILayout.Toggle (landingImpactToggle, "Simulate impact", GUILayout.ExpandWidth (true));
			GUILayout.EndHorizontal ();

			// vehicle stats
			if (landingStatsToggle) {
				GUILayout.Label ("m: " + Util.formatValue (flightData.mass, "t"));
				GUILayout.Label ("F: " + Util.formatValue (flightData.availableEngineThrustUp*1000, "N") + " (" + Util.formatValue (flightData.availableEngineThrust*1000, "N") + ")");
				GUILayout.Label ("a: " + Util.formatValue (flightData.availableEngineAccUp, "m/s^2") + " (" + Util.formatValue (flightData.availableEngineAcc, "m/s^2") + ")");
			}

			// simulate impact
			if (landingImpactToggle) {
				float simMinAltitude = 0;
				float simTime = 0;
				float simVelocity = 0;
				bool impact = Util.simulateImpact (flightData, out simMinAltitude, out simTime, out simVelocity);

				// show results
				if (impact) {
					GUILayout.Label ("Time to impact: " + Util.formatValue (simTime, "s"));
					GUILayout.Label ("Impact velocity: " + Util.formatValue (simVelocity, "m/s"));
				} else {
					GUILayout.Label ("No solution, min altitude: " + Util.formatValue (simMinAltitude, "m"));
				}
			}

			if (oldStatToggle != landingStatsToggle || oldImpactToggle != landingImpactToggle) {
				windowSizeInvalid = true;
			}
		}

		private void drawGUI ()
		{
			if(!activeSystem) return;
			int windowWidth = (windowIsMinimized)?(minimizedWindowWidth):(fullWindowWidth);

			if ((vessel == FlightGlobals.ActiveVessel) && isControllable) {
				if (windowPositionInvalid) {
					windowPositionInvalid = false;
					windowPositionAndSize.x = Screen.width - windowWidth - 10;
					windowPositionAndSize.y = 10;
				}
				if(windowSizeInvalid) {
					windowSizeInvalid = false;
					windowPositionAndSize.width = 10;
					windowPositionAndSize.height = 10;
				}
				GUI.skin = HighLogic.Skin;
				windowPositionAndSize = GUILayout.Window (windowId, windowPositionAndSize, windowGUI, "ORDA", GUILayout.MinWidth (windowWidth));	 
			}
		}

		[Serializable()]
		class GUIconfig
		{
			public PageType currentPage;
			public bool windowIsMinimized;
		}

		private void getGUIConfiguration (out GUIconfig config)
		{
			config = new GUIconfig();
			config.currentPage = currentPage;
			config.windowIsMinimized = windowIsMinimized;
		}

		private void restoreGUIConfiguration (GUIconfig config)
		{
			currentPage = config.currentPage;
			windowIsMinimized = config.windowIsMinimized;
			windowPositionInvalid = true;
			windowSizeInvalid = true;
			// save other stuff too?
		}

        #endregion

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
            print("ORDA_main.onPartAwake");
			base.onPartAwake();
		}

		protected override void onPartStart ()
		{
            print("ORDA_main.onPartStart");
			base.onPartStart();
			stackIcon.SetIcon(DefaultIcons.MYSTERY_PART);
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy();
			RenderingManager.RemoveFromPostDrawQueue(0, new Callback(drawGUI));
            vessel.OnFlyByWire -= new FlightInputCallback(fly);
		}

		public override void onBackup ()
		{
            try
            {
                base.onBackup();
                print("ORDA_main.onBackup");

                if (!flightStarted)
                    return;

                string configString = "";
                try
                {
                    // serialize gnc config
                    GNCconfig gncconfig;
                    gnc.getConfiguration(out gncconfig);
                    configString += Convert.ToBase64String(KSP.IO.IOUtils.SerializeToBinary(gncconfig)).Replace("=", "*").Replace("/", "|");

                    // serialize gui config
                    GUIconfig guiconfig;
                    getGUIConfiguration(out guiconfig);
                    configString += ";";
                    configString += Convert.ToBase64String(KSP.IO.IOUtils.SerializeToBinary(guiconfig)).Replace("=", "*").Replace("/", "|");

                }
                catch (Exception)
                {
                    customPartData = "";
                    return;
                }

                // save serialized data
                customPartData = configString;
            }
            catch
            {
            }
		}

		//
		// flight
		//
		protected override void onFlightStart ()
		{
			base.onFlightStart ();
			flightStarted = true;
			print ("ORDA_main.onFlightStart");

			// create objects
			visualHelper = new VisualHelper (this.vessel);
			flightData = new FlightData ();
			bus = new Bus ();
			propulsion = new Propulsion (flightData, bus);
			gnc = new GNC (flightData, bus);
			teleporter = new Teleporter (flightData);

			// register gui handler
			RenderingManager.AddToPostDrawQueue (0, new Callback (drawGUI));

			// try to deserialize config
			string[] configStrings = customPartData.Split (';');
			if (configStrings.Length == 2) {
				string gncConfigString = configStrings[0];
				string guiConfigString = configStrings[1];

				// gnc config
				GNCconfig gncconfig = null;
				try {
					gncconfig = (GNCconfig)KSP.IO.IOUtils.DeserializeFromBinary (Convert.FromBase64String (gncConfigString.Replace ("*", "=").Replace ("|", "/")));
				} catch (Exception) {
					print ("exception gncconfig");
				}
				if (gncconfig != null) {
					print ("restoring gnc settings");
					gnc.restoreConfiguration (gncconfig);
				}

				// gui config
				GUIconfig guiconfig = null;
				try {
					guiconfig = (GUIconfig)KSP.IO.IOUtils.DeserializeFromBinary (Convert.FromBase64String (guiConfigString.Replace ("*", "=").Replace ("|", "/")));
				} catch (Exception) {
					print ("exception guiconfig");
				}
				if(guiconfig != null) {
					print ("restoring gui settings");
					restoreGUIConfiguration(guiconfig);
				}
			}
		}

        // Called every frame
		protected override void onPartUpdate ()
		{
            try
            {
                base.onPartUpdate();

                if (!activeSystem)
                    return;
            }
            catch
            {
            }
		}

        // Called every physics step
		protected override void onPartFixedUpdate ()
		{
            try
            {
                base.onPartFixedUpdate();

                if ((this.vessel == null) || (this.vessel.packed) || (!isControllable))
                {
                    if (wasControllable)
                    {
                        wasControllable = false;
                        print("ORDA on " + getNameString() + " is no longer controllable");
                    }

                    lastFrameFly = null;
                    return;
                }
                else
                {
                    if (!wasControllable)
                    {
                        wasControllable = true;
                        print("ORDA on " + getNameString() + " is now controllable");
                    }
                }

                if (lastFrameFly.HasValue && !lastFrameFly.Value)
                {
                    //print("*********ORDA on " + getNameString() + " frame count desync (fixed update)");
                }
                else
                {
                    if (!lastFrameFly.HasValue)
                    {
                        protectionStart = System.Environment.TickCount;
                        if (!protectionOn)
                        {
                            protectionOn = true;
                        }

                        //print("*********ORDA on " + getNameString() + " frame count started on fixed update");
                    }

                    lastFrameFly = false;
                }

                float dt = Time.fixedDeltaTime;

                #region Determine Active System

                // first time, vessel changed or lost some parts?
                if (myVessel == null || myVessel != this.vessel || myVesselParts != this.vessel.parts.Count)
                {
                    // find uppermost part
                    int firstPartInverseStage = 0;
                    Part firstPart = null;
                    foreach (Part p in vessel.parts)
                    {
                        if (p is ORDA_main)
                        {
                            if (firstPart == null || p.inverseStage < firstPartInverseStage)
                            {
                                firstPart = p;
                                firstPartInverseStage = p.inverseStage;
                            }
                        }
                    }

                    // thats us?
                    if (firstPart == this)
                    {
                        // not yet active?
                        if (activeSystem == false)
                        {
                            // go active and  register fly handler
                            activeSystem = true;
                            vessel.OnFlyByWire += new FlightInputCallback(fly);
                            print("ORDA on " + getNameString() + " going active");
                        }
                        else
                        {
                            print("ORDA on " + getNameString() + " already active");
                        }
                    }
                    // not the uppermost part
                    else
                    {
                        // already active?
                        if (activeSystem == true)
                        {
                            // go inactive and remove fly handler
                            vessel.OnFlyByWire -= new FlightInputCallback(fly);
                            activeSystem = false;
                            print("ORDA on " + getNameString() + " going inactive");
                        }
                        else
                        {
                            print("ORDA on " + getNameString() + " doing nothing");
                        }
                    }

                    myVessel = this.vessel;
                    myVesselParts = this.vessel.parts.Count;
                }

                // stop if we are not the active unit
                if (!activeSystem)
                    return;

                #endregion

                // update flight data
                flightData.vessel = this.vessel;
                flightData.targetVessel = targetVessel;
                flightData.vesselPart = vesselDockingPort;
                flightData.targetPart = targetDockingPort;
                flightData.targetChanged = targetChanged;
                flightData.update();

                // guidance navigation and control update
                bus.reset();
                gnc.update(dt);
                bus.clamp();

                #region Update Visual Helpers

                // show relative velocity and position vectors?
                if ((targetVessel != null) && (relVectorsToggle))
                {
                    visualHelper.showLineInertial(3, flightData.targetRelPosition.normalized * 5);
                    visualHelper.showLineInertial(4, flightData.targetRelVelocity * 10);
                }
                else
                {
                    visualHelper.hideLine(3);
                    visualHelper.hideLine(4);
                }

                // show inertial reference frame?
                if (referenceToggle)
                {
                    visualHelper.showLineInertial(0, new Vector3(10, 0, 0));
                    visualHelper.showLineInertial(1, new Vector3(0, 10, 0));
                    visualHelper.showLineInertial(2, new Vector3(0, 0, 10));

                    wooble = false;
                }
                else if (wooble)
                {
                    visualHelper.showLineInertial(0, flightData.vessel.GetTransform().up * 50);
                    visualHelper.showLineInertial(1, flightData.totalThrustVector);
                    visualHelper.showLineInertial(2, flightData.attError * 50);
                }
                else
                {
                    visualHelper.hideLine(0);
                    visualHelper.hideLine(1);
                    visualHelper.hideLine(2);
                    visualHelper.hideLine(5);
                    visualHelper.hideLine(6);
                }

                #endregion

                // magic teleporter
                if (enableCheats)
                {
                    teleporter.update(dt);
                }
            }
            catch
            {
            }
		}

		//
		// pausing
		//
		protected override void onGamePause ()
		{
			base.onGamePause();
		}
		
		protected override void onGameResume()
		{
			base.onGameResume();
		}

		//
		// ...
		//
		protected override void onDisconnect ()
		{
			base.onDisconnect();
            vessel.OnFlyByWire -= new FlightInputCallback(fly);
            print("ORDA on " + getNameString() + " going inactive");
		}

		//
		// utils
		//
		private string getNameString ()
		{
            string name = "[";

            if (this.vessel != null)
            {
                name += this.vessel.vesselName + ";" + vessel.parts.Count + ";";
            }

            name += this.inverseStage + ";" + this.gameObject.name + "]";

            return name;
		}
	}
}