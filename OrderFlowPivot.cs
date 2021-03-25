namespace NinjaTrader.NinjaScript.Strategies
{
	public class OrderFlowPivots : Strategy
	{
		// Declaration of necessary variables
		private double priorHigh 	= 0;
		private double priorLow		= 0;
		private double priorClose	= 0;
		private double currentOpen 	= 0;
		private double currentLow 	= 0;
		private double currentHigh 	= 0;
		private double openPrice 	= 0;
		private Order entry1 = null;
		private Order entry2 = null;
		private Order entry3 = null;
		private Order entry4 = null;
		private Order stop1 = null;
		private Order stop2 = null;
		private Order stop3 = null;
		private Order stop4 = null;
		private Order target1 = null;
		private Order target2 = null;
		private Order target3 = null;
		private Order target4 = null;
		private int sumFilled = 0;
		
		// Arrays used throughout the code
		private double[] supports = new double[0];
		private double[] resistances = new double[0];
		private int[,] stopTargetTicks = new int[,] { {5, 4}, {3, 6}, {3, 7}, {3, 8} }; 		// Int Array of {# of stop ticks, # of target ticks}
		private Order[] allOrders;
		private Order[] stopOrders;
		private Order[] targetOrders;
		
		//String names for order objects
		private string[] entryStrings = new String[] { "entry1", "entry2", "entry3", "entry4" };  
		private string[] stopStrings = new String[] { "stop1", "stop2", "stop3", "stop4" };
		private string[] targetStrings = new String[] { "target1", "target2", "target3", "target4" };

		
		private string[] allPivots = new string[0];
		
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "OrderFlowPivots";
				Calculate									= Calculate.OnEachTick;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Day;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				
				
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
			} 
			else if (State == State.Realtime) {
                // one time only, as we transition from historical
                // convert any old historical order object references
                // to the new live order submitted to the real-time account
				for (int index = 0; index < allOrders.Length; ) { 
	                if (allOrders[index] != null)
	                    allOrders[index] = GetRealtimeOrder(allOrders[index]);
	                if (stopOrders[index] != null)
	                    stopOrders[index] = GetRealtimeOrder(stopOrders[index]);
	                if (targetOrders[index] != null)
				       targetOrders[index] = GetRealtimeOrder(targetOrders[index]);
            	}
			}
			else if (State == State.Configure)
			{	
				//Adds data series for daily charts
				AddDataSeries("ES 06-21", BarsPeriodType.Day, 1);
				
				// Sets the values for the prior day's high, low, and close
				if (PriorDayOHLC().PriorHigh[1] > 0) {
				    priorHigh = PriorDayOHLC().PriorHigh[1];
				    priorLow = PriorDayOHLC().PriorLow[1];
					priorClose = PriorDayOHLC().PriorClose[1];
				}
				
				allOrders = new Order[] { target1, target2, target3, target4 };
				stopOrders = new Order[] { stop1, stop2, stop3, stop4 };
				targetOrders = new Order[] { target1, target2, target3, target4 };
				
			} 
		}
		
		protected override void OnBarUpdate() {
			
			//Sets the current high/low
			
		}
		
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale) {
		  base.OnRender(chartControl, chartScale);
		  // loop through only the rendered bars on the chart
		  for(int barIndex = ChartBars.FromIndex; barIndex <= ChartBars.ToIndex; barIndex++)
		  {
		    // get the open price at the selected bar index value
		    double openPrice = Bars.GetOpen(barIndex);
		  }
		}
		
		protected void OnTickUpdate() {
			
			double[] allPivots = new double[] { priorHigh, priorLow, priorClose, currentOpen, currentHigh, currentLow };
			
			/* If the current price is greater than the pivot point, add it to the supports array
			   If the current price is less than the pivot point, add it to the resistance array    */ 
			for (int index = 0; index < allPivots.Length; index++) {
				if (allPivots[index] > GetCurrentBid()) {
					Array.Resize(ref resistances, resistances.Length + 1);
					resistances[resistances.GetUpperBound(0)] = allPivots[index];
					Array.Sort(resistances);
				} 
				else if (allPivots[index] < GetCurrentBid()) {
					Array.Resize(ref supports, supports.Length + 1);
					supports[supports.GetUpperBound(0)] = allPivots[index];
					Array.Sort(supports);
			}
		}
			
			/* Loop through the supports array and if the current price is within 12 Ticks (3 points)
			   of the support price, we are going to place a limit order at the support price and one tick below    */
			for (int index = 0; index < supports.Length; index++) {
				if ((GetCurrentBid() - supports[index]) <= (12 * TickSize) && Position.MarketPosition == MarketPosition.Flat) {
					//Order and Exit Target for first buy
					EnterLongLimit(1, supports[index], "entry1");
					//Order and Exit for Second Buy
					EnterLongLimit(1, supports[index] - TickSize, "entry2");
					//Order and Exits for Third Buy
					EnterLongLimit(1, supports[index] - (2 * TickSize), "entry3");
					EnterLongLimit(1, supports[index] - (2 * TickSize), "entry4");
					
				}
			}
			
			for (int index = 0; index < stopOrders.Length; index++) {
				/*  If we have a long position and the current price is 4 ticks in profit, raise the stop loss order to breakeven  */
				if (Position.MarketPosition == MarketPosition.Long && Close[0] >= Position.AveragePrice + (4 * TickSize)) {	
					// Checks to see if our Stop order has been submitted already
		            if (stopOrders[index] != null && stopOrders[index].StopPrice < Position.AveragePrice) {
						// Modifies stop-loss to breakeven
		                stopOrders[index] = ExitLongStopMarket(0, true, stopOrders[index].Quantity, Position.AveragePrice, stopStrings[index], entryStrings[index]);
					}
				}
			}
		}
		
		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError) {
			
            // Handle entry orders here. The entry object allows us to identify that the order that is calling the OnOrderUpdate() method is the entry order.
            // Assign entryOrder in OnOrderUpdate() to ensure the assignment occurs when expected.
            // This is more reliable than assigning Order objects in OnBarUpdate, as the assignment is not gauranteed to be complete if it is referenced immediately after submitting
	            if (order.Name == "entry1") {
	                entry1 = order;
	                // Reset the entryOrder object to null if order was cancelled without any fill
	                if (order.OrderState == OrderState.Cancelled && order.Filled == 0) {
	                    entry1 = order;
	                    sumFilled = 0;
	                }
	            }
				if (order.Name == "entry2") {
	                entry2 = order;
	                // Reset the entryOrder object to null if order was cancelled without any fill
	                if (order.OrderState == OrderState.Cancelled && order.Filled == 0) {
	                    entry2 = order;
	                    sumFilled = 0;
	                }
	            }
				if (order.Name == "entry3") {
	                entry3 = order;
	                // Reset the entryOrder object to null if order was cancelled without any fill
	                if (order.OrderState == OrderState.Cancelled && order.Filled == 0) {
	                    entry3 = order;
	                    sumFilled = 0;
	                }
	            }
				if (order.Name == "entry4") {
	                entry4 = order;
	                // Reset the entryOrder object to null if order was cancelled without any fill
	                if (order.OrderState == OrderState.Cancelled && order.Filled == 0) {
	                    entry4 = order;
	                    sumFilled = 0;
	                }
        		}
		}

		
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time) {
			
			
			
			/* Monitoring OnExecution to trigger the submission of stop/longExit orders instead of OnOrderUpdate() since OnExecution() is called after OnOrderUpdate()
			   which ensures that the strategy has received the execution which is used for internal signal tracking      */
			for (int index = 0; index < allOrders.Length; index++) {
				if ( allOrders[index] != null && allOrders[index] == execution.Order) {
					if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)) {
						
						// We sum the quantities of each execution making up the entry order
						sumFilled += execution.Quantity;
						
						//Submit stop limit and long exit orders orders for partial fills
						if (execution.Order.OrderState == OrderState.PartFilled) {
							stopOrders[index] = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice - (stopTargetTicks[index, 0] * TickSize)), stopStrings[index], entryStrings[index]);
							targetOrders[index] = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice - (stopTargetTicks[index, 1] * TickSize), targetStrings[index], entryStrings[index]);
						}

						//Update the exit order quantities once understate turns to filled and we have seen execution quantities match order quantities
						else if (execution.Order.OrderState == OrderState.Filled && sumFilled == execution.Order.Filled) {
							//Stop Loss Order for OrderState.Filled
							stopOrders[index] = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice - (stopTargetTicks[index, 0] * TickSize)), stopStrings[index], entryStrings[index]);
							targetOrders[index] = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice - (stopTargetTicks[index, 1] * TickSize), targetStrings[index], entryStrings[index]);
						}
						
						//Reset the entryOrder object and the sumFilled counter to null / 0 after the order has been filled
						if (execution.Order.OrderState != OrderState.PartFilled && sumFilled == execution.Order.Filled) {
							allOrders[index] = null;
							sumFilled = 0;
						}
					}
				}
				
				/* Reset the stop orders and target orders Order objects after our position has been closed */
				if ((stopOrders[index] != null && stopOrders[index] == execution.Order) || (targetOrders[index] != null && targetOrders[index] == execution.Order)) {
					if (execution.Order.OrderState != OrderState.PartFilled || execution.Order.OrderState == OrderState.PartFilled) {
						stopOrders[index] = null;
						targetOrders[index] = null;
					}
				}
			}
		}
		
	
	
	}
}
	
