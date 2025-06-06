﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using Point = System.Drawing.Point;

namespace OsEngine.Market
{
    public class ServerMasterOrdersPainter
    {
        public ServerMasterOrdersPainter()
        {
            _currentCulture = OsLocalization.CurCulture;
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;

            Task task = new Task(PainterThreadArea);
            task.Start();
        }

        private CultureInfo _currentCulture;

        private void ServerMaster_ServerCreateEvent(IServer server)
        {
            if (server.ServerType == ServerType.Optimizer)
            {
                return;
            }

            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; i < servers.Count; i++)
            {
                try
                {
                    if (servers[i] == null)
                    {
                        continue;
                    }
                    if (servers[i].ServerType == ServerType.Optimizer)
                    {
                        continue;
                    }
                    servers[i].NewOrderIncomeEvent -= server_NewOrderIncomeEvent;
                    servers[i].NewMyTradeEvent -= server_NewMyTradeEvent;

                    servers[i].NewOrderIncomeEvent += server_NewOrderIncomeEvent;
                    servers[i].NewMyTradeEvent += server_NewMyTradeEvent;
                }
                catch
                {
                    // ignore
                }

            }
        }

        public void StartPaint()
        {
            if (_hostActiveOrders.Dispatcher.CheckAccess() == false)
            {
                _hostActiveOrders.Dispatcher.Invoke(new Action(StartPaint));
                return;
            }

            try
            {

                if (_hostActiveOrders != null)
                {
                    _hostActiveOrders.Child = _gridActiveOrders;
                }

                if (_hostHistoricalOrders != null)
                {
                    _hostHistoricalOrders.Child = _gridHistoricalOrders;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {

            if (_hostActiveOrders != null)
            {
                _hostActiveOrders.Child = null;
            }

            if (_hostHistoricalOrders != null)
            {
                _hostHistoricalOrders.Child = null;
            }

        }

        public void SetHostTable(WindowsFormsHost hostActiveOrders, WindowsFormsHost hostHistoricalOrders)
        {
            try
            {
                if (hostActiveOrders != null)
                {
                    _gridActiveOrders = DataGridFactory.GetDataGridOrder();
                    _gridActiveOrders.ScrollBars = ScrollBars.Vertical;
                    for (int i = 1; i < _gridActiveOrders.Columns.Count; i++)
                    {
                        _gridActiveOrders.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _hostActiveOrders = hostActiveOrders;
                    _hostActiveOrders.Child = _gridActiveOrders;
                    _gridActiveOrders.Click += _gridOrders_Click;
                }

                if (hostHistoricalOrders != null)
                {
                    _hostHistoricalOrders = hostHistoricalOrders;
                    _gridHistoricalOrders = DataGridFactory.GetDataGridOrder();
                    _gridHistoricalOrders.ScrollBars = ScrollBars.Vertical;

                    for (int i = 1; i < _gridHistoricalOrders.Columns.Count; i++)
                    {
                        _gridHistoricalOrders.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _hostHistoricalOrders.Child = _gridHistoricalOrders;
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void InsertOrder(Order order)
        {
            server_NewOrderIncomeEvent(order);
        }

        #region Drawing order thread

        private async void PainterThreadArea()
        {
            while (true)
            {
                await Task.Delay(5000);

                try
                {

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_needToPaintOrders)
                    {
                        _needToPaintOrders = false;

                        //_orders

                        List<Order> activeOrders = new List<Order>();

                        List<Order> historicalOrders = new List<Order>();

                        for (int i = 0; i < _orders.Count; i++)
                        {
                            Order order = _orders[i];

                            if(order == null)
                            {
                                continue;
                            }

                            if (order.State == OrderStateType.Active
                                || order.State == OrderStateType.Pending
                                || order.State == OrderStateType.None)
                            {
                                activeOrders.Add(order);
                            }
                            else
                            {
                                historicalOrders.Add(order);
                            }
                        }

                        SortOrders(activeOrders);
                        SortOrders(historicalOrders);

                        // высылаем на прорисовку отдельно

                        if (_gridActiveOrders != null)
                        {
                            PaintOrders(activeOrders, _gridActiveOrders);
                        }

                        if (_gridHistoricalOrders != null)
                        {
                            PaintOrders(historicalOrders, _gridHistoricalOrders);
                        }
                    }

                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SortOrders(List<Order> orders)
        {
            if (orders.Count > 1)
            { // Ура, пузырик!

                for (int i = 0; i < orders.Count; i++)
                {
                    for (int i2 = 1; i2 < orders.Count; i2++)
                    {
                        if (orders[i2].NumberUser < orders[i2 - 1].NumberUser)
                        {
                            Order order = orders[i2];
                            orders[i2] = orders[i2 - 1];
                            orders[i2 - 1] = order;
                        }
                    }
                }
            }
        }

        private bool _needToPaintOrders;

        #endregion

        #region Drawing work

        private DataGridView _gridActiveOrders;

        private WindowsFormsHost _hostActiveOrders;

        private DataGridView _gridHistoricalOrders;

        private WindowsFormsHost _hostHistoricalOrders;

        private object _lockerOrders = new Object();

        private void server_NewOrderIncomeEvent(Order order)
        {
            if (order.ServerType == ServerType.Optimizer ||
                order.ServerType == ServerType.Miner)
            {
                return;
            }

            try
            {
                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

                lock (_lockerOrders)
                {
                    Order myOrder = null;

                    for(int i = 0;i < _orders.Count;i++)
                    {
                        Order curOrder = _orders[i];

                        if (curOrder == null)
                        {
                            continue;
                        }

                        if(curOrder.NumberUser != 0 &&
                            order.NumberUser != 0 
                            && curOrder.NumberUser == order.NumberUser)
                        {
                            myOrder = curOrder;
                            break;
                        }
                        if(string.IsNullOrEmpty(curOrder.NumberMarket) == false &&
                            string.IsNullOrEmpty(order.NumberMarket) == false &&
                            curOrder.NumberMarket == order.NumberMarket)
                        {
                            myOrder = curOrder;
                            break;
                        }
                    }

                    if (myOrder == null)
                    {
                        _orders.Add(order);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(order.NumberMarket))
                        {
                            myOrder.NumberMarket = order.NumberMarket;
                        }

                        if (order.Price != 0)
                        {
                            myOrder.Price = order.Price;
                        }

                        if (order.Side != Side.None)
                        {
                            myOrder.Side = order.Side;
                        }

                        if (!string.IsNullOrWhiteSpace(order.PortfolioNumber))
                        {
                            myOrder.PortfolioNumber = order.PortfolioNumber;
                        }

                        if (order.Volume != 0)
                        {
                            myOrder.Volume = order.Volume;
                        }

                        if (order.VolumeExecute != 0)
                        {
                            myOrder.VolumeExecute = order.VolumeExecute;
                        }

                        if (order.State != OrderStateType.None)
                        {
                            myOrder.State = order.State;
                        }

                        if (string.IsNullOrWhiteSpace(myOrder.SecurityNameCode))
                        {
                            myOrder.SecurityNameCode = order.SecurityNameCode;
                        }
                        if (myOrder.TimeCallBack == DateTime.MinValue)
                        {
                            myOrder.TimeCallBack = order.TimeCallBack;
                        }
                        if (myOrder.TimeCreate == DateTime.MinValue)    //AVP
                        {
                            myOrder.TimeCreate = order.TimeCreate;
                        }
                    }

                    
                    if (_orders.Count > 200)
                    {
                        _orders.RemoveAt(0);
                    }
                }
            }
            catch (Exception error)
            {
                _orders.Clear();
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            _needToPaintOrders = true;
        }

        private object _lockerTrades = new Object();

        private void server_NewMyTradeEvent(MyTrade trade)
        {
            if (_orders == null || _orders.Count == 0)
            {
                return;
            }

            lock (_lockerTrades)
            {
                Order myOrder = _orders.Find(order1 => order1.NumberMarket == trade.NumberOrderParent);

                if (myOrder == null)
                {
                    return;
                }

                if (myOrder.ServerType == ServerType.Tester ||
                    myOrder.ServerType == ServerType.Optimizer ||
                    myOrder.ServerType == ServerType.Miner)
                {
                    return;
                }

                _needToPaintOrders = true;
            }
        }

        private List<Order> _orders;

        private void PaintOrders(List<Order> ordersToPaint, DataGridView gridToPaint)
        {
            try
            {
                if (gridToPaint == null)
                {
                    return;
                }

                if (gridToPaint.InvokeRequired)
                {
                    gridToPaint.Invoke(new Action<List<Order>, DataGridView>(PaintOrders), ordersToPaint, gridToPaint);
                    return;
                }
                gridToPaint.Rows.Clear();

                if (ordersToPaint == null ||
                    ordersToPaint.Count == 0)
                {
                    return;
                }

                for (int i = ordersToPaint.Count - 1; ordersToPaint != null && ordersToPaint.Count != 0 && i > -1; i--)
                {
                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = ordersToPaint[i].NumberUser;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = ordersToPaint[i].NumberMarket;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = ordersToPaint[i].TimeCreate.ToString(_currentCulture);

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = ordersToPaint[i].SecurityNameCode;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = ordersToPaint[i].PortfolioNumber;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = ordersToPaint[i].Side;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[6].Value = ordersToPaint[i].State;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[7].Value = ordersToPaint[i].Price.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[8].Value = ordersToPaint[i].PriceReal.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[9].Value = ordersToPaint[i].Volume.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[10].Value = ordersToPaint[i].TypeOrder;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());

                    if (ordersToPaint[i].TimeRoundTrip > new TimeSpan(0, 0, 0, 0))
                    {
                        nRow.Cells[11].Value = ordersToPaint[i].TimeRoundTrip;
                    }

                    gridToPaint.Rows.Add(nRow);
                }
            }
            catch
            {
                // ignore. Let us sometimes face with null-value, when deleting the original order or modification, but don't break work of mail thread
                // игнорим. Пусть иногда натыкаемся на налл, при удалении исходного ордера или модификации
                // зато не мешаем основному потоку работать
                //SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridOrders_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                ToolStripMenuItem[] items = new ToolStripMenuItem[2];

                items[0] = new ToolStripMenuItem { Text = OsLocalization.Market.Message4 };

                items[0].Click += OrdersCloseAll_Click;

                items[1] = new ToolStripMenuItem { Text = OsLocalization.Market.Message5 };
                items[1].Click += PositionCloseForNumber_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items);

                _gridActiveOrders.ContextMenuStrip = menu;
                _gridActiveOrders.ContextMenuStrip.Show(_gridActiveOrders, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ClearOrders()
        {
            _orders = new List<Order>();
        }

        private void OrdersCloseAll_Click(object sender, EventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label67);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            new Task(() =>
            {
                try
                {
                    if (_orders != null)
                    {
                        for (int i = 0; i < _orders.Count; i++)
                        {
                            Order order = _orders[i];

                            if (order.State == OrderStateType.Active &&
                                !string.IsNullOrEmpty(order.PortfolioNumber))
                            {
                                if (order.PortfolioNumber == "Emulator")
                                {
                                    if(RevokeOrderToEmulatorEvent != null)
                                    {
                                        RevokeOrderToEmulatorEvent(order);
                                    }
                                }
                                else
                                {
                                    if (ServerMaster.GetServers() ==  null)
                                    {
                                        continue;
                                    }

                                    IServer server = null;

                                    if (string.IsNullOrEmpty(order.ServerName) == false)
                                    {
                                        server = ServerMaster.GetServers().Find(server1 =>
                                        server1.ServerNameAndPrefix == order.ServerName);
                                    }
                                    else
                                    {
                                        server = ServerMaster.GetServers().Find(server1 =>
                                        server1.ServerType == order.ServerType);
                                    }

                                    if (server != null)
                                    {
                                        server.CancelOrder(order);
                                    }
                                }
                            }
                        }
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    for (int i = 0; servers != null && i < servers.Count; i++)
                    {
                        IServer server = servers[i];
                        server.CancelAllOrders();
                    }

                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }).Start();
        }

        private void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label68);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            new Task(() =>
            {
                try
                {
                    if (_orders == null || _orders.Count == 0)
                    {
                        return;
                    }

                    if (_gridActiveOrders.Rows == null ||
                        _gridActiveOrders.Rows.Count == 0 ||
                        _gridActiveOrders.CurrentCell == null)
                    {
                        return;
                    }

                    Order order = _orders[(_orders.Count - 1 - _gridActiveOrders.CurrentCell.RowIndex)];    // иногда ошибается, не тот ордер возвращает
                    try   //AVP
                    {
                        int ordNumber = (int)_gridActiveOrders.Rows[_gridActiveOrders.CurrentCell.RowIndex].Cells[0].Value;    
                        if (order.NumberUser != ordNumber)
                        {
                            for (int i = 0; i < _orders.Count; i++)
                            {
                                if (_orders[i].NumberUser == ordNumber)
                                {
                                    order = _orders[i];
                                    break;
                                }
                            }
                        }
                    }
                    catch 
                    {
                        if (order is null)
                        {
                            return;
                        }
                    }
                    if ((order.State == OrderStateType.Active || order.State == OrderStateType.Pending)
                        &&
                            !string.IsNullOrEmpty(order.PortfolioNumber))
                    {
                        if (order.PortfolioNumber == "Emulator")
                        {
                            if (RevokeOrderToEmulatorEvent != null)
                            {
                                RevokeOrderToEmulatorEvent(order);
                            }
                        }
                        else
                        {
                            IServer server = null;

                            if(string.IsNullOrEmpty(order.ServerName) == false)
                            {
                                server = ServerMaster.GetServers().Find(server1 => 
                                server1.ServerNameAndPrefix == order.ServerName);
                            }
                            else
                            {
                                server = ServerMaster.GetServers().Find(server1 => 
                                server1.ServerType == order.ServerType);
                            }

                            if (server != null)
                            {
                                server.CancelOrder(order);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }).Start();

        }

        public event Action<Order> RevokeOrderToEmulatorEvent;

        #endregion

        #region Drawing work

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is substribed to us and there is a log error / если на нас никто не подписан и в логе ошибка
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}