﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NativeLibAllocationTrace
{
    public partial class MainForm : Form
    {
        class PointerStatistics
        {
            public int Count { get; set; }

            public long MemorySize { get; set; }

            public long TotalCount { get; set; }

            public void Reset()
            {
                Count = 0;
                MemorySize = 0;
                TotalCount = 0;
            }
        }

        class PointerInfoTag
        {
            public long Pointer { get; }

            public long SizeOfPointerData { get; }

            public PointerInfoTag(long pointer, long sizeOfPointerData)
            {
                Pointer = pointer;
                SizeOfPointerData = sizeOfPointerData;
            }
        }

        private readonly RpcServer _rpc = new RpcServer();
        private readonly Timer _timer;
        private readonly Dictionary<long, PointerData> _pointerDataMap = new Dictionary<long, PointerData>();

        private readonly PointerStatistics _pointerStatistics = new PointerStatistics();

        public MainForm()
        {
            _rpc.OnEvent += OnRpcEventAsync;

            InitializeComponent();

            Resize += (sender, e) => UpdateSizing();
            UpdateSizing();
            UpdatePointerStatistics();

            var lvi = new ListViewItem($"162.394");
            lvi.SubItems.Add(@"D:\Coding\CPP\MyLibs\nativelib\tool\NativeLibAllocationTrace\src\test.cpp:359");
            lvi.SubItems.Add("CMyClassName::AllocateOverlapped");
            lvi.SubItems.Add($"0xCCDA23AF38D9040D");
            lvTraces.Items.Add(lvi);

            //throw new Exception("ask for pointer symbolic info");
            _timer = new Timer()
            {
                Interval = 100,
                Enabled = true
            };
            _timer.Tick += _timer_Tick;
        }

        private async void _timer_Tick(object sender, EventArgs e)
        {
            (sender as Timer).Enabled = false;

            await _rpc.DispatchEventsAsync();

            (sender as Timer).Enabled = true;
        }

        private async Task<PointerData> GetPointerDataAsync(long pointer)
        {
            if (!_pointerDataMap.TryGetValue(pointer, out PointerData pointerData))
            {
                pointerData = await _rpc.QueryFunctionPointerData(pointer);
                _pointerDataMap.Add(pointer, pointerData);

                Debug.WriteLine($"Resolved function pointer 0x{pointer.ToString("x8")} ==> {pointerData.Function}");
            }

            return pointerData;
        }

        private DateTime _nextProcessMessages = DateTime.UtcNow;
        private readonly Dictionary<long, ListViewItem> _lviMap = new Dictionary<long, ListViewItem>();
        private async Task OnRpcEventAsync(RpcEvent ev)
        {
            var now = DateTime.UtcNow;
            if (now >= _nextProcessMessages)
            {
                Application.DoEvents();
                _nextProcessMessages = now.AddMilliseconds(1000);
            }

            if (ev is ConnectedRpcEvent)
            {
                lvTraces.Items.Clear();
                _lviMap.Clear();
                _pointerDataMap.Clear();
                _pointerStatistics.Reset();
                UpdatePointerStatistics();
            }
            else if (ev is DisconnectedRpcEvent)
            {
            }
            else if (ev is AddAllocationRpcEvent addAllocationEvent)
            {
                /*var pointerData = GetPointerDataAsync(addAllocationEvent.Pointer);
                foreach (var ptr in addAllocationEvent.Stack)
                {
                    var info = await GetPointerDataAsync(ptr);
                    // ...
                }*/

                var lvi = new ListViewItem($"{addAllocationEvent.Time:0.000}");
                lvi.Tag = new PointerInfoTag(addAllocationEvent.Pointer, addAllocationEvent.SizeOfPointerData);
                lvi.SubItems.Add($"{addAllocationEvent.Filename}:{addAllocationEvent.Line}");
                lvi.SubItems.Add(addAllocationEvent.Function);
                lvi.SubItems.Add($"0x{addAllocationEvent.Pointer.ToString("X8")} ({addAllocationEvent.SizeOfPointerData} B)");
                lvTraces.Items.Add(lvi);

                if (_lviMap.ContainsKey(addAllocationEvent.Pointer))
                    throw new Exception($"Duplicated key added!!");

                _lviMap.Add(addAllocationEvent.Pointer, lvi);

                _pointerStatistics.Count++;
                _pointerStatistics.MemorySize += addAllocationEvent.SizeOfPointerData;
                _pointerStatistics.TotalCount++;
                UpdatePointerStatistics();
            }
            else if (ev is RemoveAllocationRpcEvent removeAllocationEvent)
            {
                if (!_lviMap.TryGetValue(removeAllocationEvent.Pointer, out ListViewItem lvi))
                    throw new Exception("lvi not found, ptr: 0x" + removeAllocationEvent.Pointer.ToString("x8"));

                var check = lvTraces.Items.Count;

                var tag = lvi.Tag as PointerInfoTag;
                lvi.Remove();
                _lviMap.Remove(removeAllocationEvent.Pointer);

                if (lvTraces.Items.Count == check)
                    throw new Exception($"inv1 {lvTraces.Items.Count} == {check}");

                _pointerStatistics.Count--;
                _pointerStatistics.MemorySize -= tag.SizeOfPointerData;

                if (_pointerStatistics.Count != lvTraces.Items.Count)
                {
                    throw new Exception($"inv2: {_pointerStatistics.Count} != {lvTraces.Items.Count}");
                }

                if (_pointerStatistics.Count == 0 &&
                    _pointerStatistics.MemorySize != 0)
                {
                    throw new Exception($"inv3 memorysize: {_pointerStatistics.MemorySize}");
                }

                UpdatePointerStatistics();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _rpc.Dispose();

                if (components != null)
                    components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void UpdateSizing()
        {
            int width = lvTraces.ClientSize.Width;

            lvTraces.Columns[0].Width = (int)(width * 0.1);
            lvTraces.Columns[1].Width = (int)(width * 0.5);
            lvTraces.Columns[2].Width = (int)(width * 0.2);
            lvTraces.Columns[3].Width = (int)(width * 0.2);
        }

        private void UpdatePointerStatistics()
        {
            tslPointerCount.Text = $"{_pointerStatistics.Count} pointers";
            tslMemorySize.Text = $"{_pointerStatistics.MemorySize} B";
            tslTotalAllocations.Text = $"{_pointerStatistics.TotalCount} total allocations";
        }
    }
}