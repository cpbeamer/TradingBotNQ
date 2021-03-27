#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class OrderFlowPivots : Strategy
	{
		// Declaration of all the potential pivots
		private double priorHigh;
		private double priorLow;
		private double priorClose;
		private double currentOpen;
		private double currentLow;
		private double currentHigh;
		private double openPrice;
		private double currentVAL;
		private double currentVAH;
		private double priorVAL;
		private double priorVAH;
		
		// Set up the order, traget, and stop variables
		private Order entryOrder = null;
		private Order stopOrder = null;
		private Order targetOrder = null;
		private int sumFilled = 0;
		private int barNumberOfOrder = 0;
		
		
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Automated trading bot that uses Orderflow pivots to scalp";
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
				OffsetFromPivot								= 1;
				StopLossTicks								= 10;
				ProfitTargetTicks							= 4;
				
				
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
				
			} 
			else if (State == State.Configure)
			{
				
				//Adds data series for daily charts
				AddDataSeries("ES 06-21", BarsPeriodType.Day, 1);

			} 
			else if (State == State.DataLoaded) 
			{
				
   				if (CurrentBar < 20)
       				return;
				
				// Sets the values for the prior day's high, low, and close
				if (PriorDayOHLC().PriorHigh[1] > 0) {
				    priorHigh = PriorDayOHLC().PriorHigh[1];
				    priorLow = PriorDayOHLC().PriorLow[1];
					priorClose = PriorDayOHLC().PriorClose[1];
				}
				
				//Calculate the VAH and VAL of the prior day
				priorVAH = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAt[1];
				priorVAL = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAb[1];
				
				//Get data for the current open
				currentOpen = PriorDayOHLC().Open[1];
			}
		}
		
		
		protected override void OnBarUpdate() {
			
			
   			 if (CurrentBar < 20)
        		return;
			
			double[] allPivots = new double[] { priorHigh, priorLow, priorClose, currentOpen, currentHigh, currentLow, openPrice, currentVAL, currentVAH, priorVAL, priorVAH };
			
			if (BarsInProgress == 0) {
				/* Loop through the supports array and if the current price is within 10 Ticks (2.5 points)
			   of the support price, we are going to place a limit order at the support price and one tick below    */
				for (int index = 0; index < allPivots.Length; index++) {
					if ((GetCurrentBid() - allPivots[index]) <= (10 * TickSize) && Position.MarketPosition == MarketPosition.Flat) {
						EnterLongLimit(1, allPivots[index] - (OffsetFromPivot * TickSize), "entry1");
					}
					else if ((allPivots[index] - GetCurrentBid()) >= (10 * TickSize) && Position.MarketPosition == MarketPosition.Flat) {
						EnterShortLimit(1, allPivots[index] + (OffsetFromPivot * TickSize), "entry1");
						}
					}
			
				/*  If we have a long position and the current price is 4 ticks in profit, raise the stop loss order to breakeven  */
				if (Position.MarketPosition == MarketPosition.Long && Close[0] >= Position.AveragePrice + (8 * TickSize) || Position.MarketPosition == MarketPosition.Short && Close[0] >= Position.AveragePrice - (8 * TickSize) ) {	
					// Checks to see if our Stop order has been submitted already
		            if (stopOrder != null && stopOrder.StopPrice < Position.AveragePrice) {
						// Modifies stop-loss to breakeven
		                stopOrder = ExitLongStopMarket(0, true, stopOrder.Quantity, Position.AveragePrice, "MyStop", "entry1");
					}
				}
				
				currentVAH = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAt[0];
				currentVAL = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAb[0];
				
				}
			
				else if (BarsInProgress == 1) {
					
					currentHigh = High[1];
					currentLow = Low[1];
					currentOpen = Open[1];
				}		
			}
		
		
		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError) {
			
	        /* Handle entry orders here. The entry object allows us to identify that the order that is calling the OnOrderUpdate() method is the entry order.
	           Assign entryOrder in OnOrderUpdate() to ensure the assignment occurs when expected.
	           is more reliable than assigning Order objects in OnBarUpdate, as the assignment is not gauranteed to be complete if it is referenced immediately after submitting */
            if (order.Name == "entry1") {
                entryOrder = order;
                // Reset the entryOrder object to null if order was cancelled without any fill
                if (order.OrderState == OrderState.Cancelled && order.Filled == 0) {
                    entryOrder = order;
                    sumFilled = 0;
                }
            }
		}

		
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time) {	
			
			/* Monitoring OnExecution to trigger the submission of stop/longExit orders instead of OnOrderUpdate() since OnExecution() is called after OnOrderUpdate()
			   which ensures that the strategy has received the execution which is used for internal signal tracking      */
				if ( entryOrder != null && entryOrder == execution.Order) {
					if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)) {
						
						/* We sum the quantities of each execution making up the entry order */
						sumFilled += execution.Quantity;
						
						/*Submit stop limit and long exit orders orders for partial fills
						  These functions handle the entries for all of our long positions */
						if (Position.MarketPosition == MarketPosition.Long) {
							if (execution.Order.OrderState == OrderState.PartFilled) {
								stopOrder = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice - (StopLossTicks * TickSize)), "MyStop", "entry1");
								targetOrder = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice - (ProfitTargetTicks * TickSize), "MyTarget", "entry1");
							}
							//Update the exit order quantities once understate turns to filled and we have seen execution quantities match order quantities
							else if (execution.Order.OrderState == OrderState.Filled && sumFilled == execution.Order.Filled) {
								
								//Stop Loss Order for OrderState.Filled
								stopOrder = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice - (StopLossTicks * TickSize)), "MyStop", "entry1");
								targetOrder = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice - (ProfitTargetTicks * TickSize), "MyTarget", "entry1");
							}
							//Reset the entryOrder object and the sumFilled counter to null / 0 after the order has been filled
							if (execution.Order.OrderState != OrderState.PartFilled && sumFilled == execution.Order.Filled) {
								entryOrder = null;
								sumFilled = 0;
							}
						}
						
						/*Submit stop limit and short exit orders orders for partial fills
						  These functions handle the entries for all of our short positions */
						if (Position.MarketPosition == MarketPosition.Short) {
							if (execution.Order.OrderState == OrderState.PartFilled) {
								stopOrder = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice + (StopLossTicks * TickSize)), "MyStop", "entry1");
								targetOrder = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice + (ProfitTargetTicks * TickSize), "MyTarget", "entry1");
							}
							//Update the exit order quantities once understate turns to filled and we have seen execution quantities match order quantities
							else if (execution.Order.OrderState == OrderState.Filled && sumFilled == execution.Order.Filled) {
								//Stop Loss Order for OrderState.Filled
								stopOrder = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice + (StopLossTicks * TickSize)), "MyStop", "entry1");
								targetOrder = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice + (ProfitTargetTicks * TickSize), "MyTarget", "entry1");
							}
							//Reset the entryOrder object and the sumFilled counter to null / 0 after the order has been filled
							if (execution.Order.OrderState != OrderState.PartFilled && sumFilled == execution.Order.Filled) {
								entryOrder = null;
								sumFilled = 0;
							}
						}	
					}
				}
				
				/* Reset the stop orders and target orders Order objects after our position has been closed */
				if ((stopOrder != null && stopOrder == execution.Order) || (targetOrder != null && targetOrder == execution.Order)) {
					if (execution.Order.OrderState != OrderState.PartFilled || execution.Order.OrderState == OrderState.PartFilled) {
						stopOrder = null;
						targetOrder = null;
					}
				}
			}
		
	
		#region Properties
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "StopLossTicks", GroupName = "NinjaScriptStrategyParameters", Order = 1)]
		public int StopLossTicks
		{ get; set; }
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "ProfitTargetTicks", GroupName = "NinjaScriptStrategyParameters", Order = 1)]
		public int ProfitTargetTicks
		{ get; set; }
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "OffsetFromPivot", GroupName = "NinjaScriptStrategyParameters", Order = 1)]
		public int OffsetFromPivot
		{ get; set; }
		#endregion
	
	}
}
	

	
