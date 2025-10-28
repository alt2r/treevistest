using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace tree_diagram_visualisation_test.Controls
{
    public class TreeDiagram : Control
    {
        DrawingContext context;
        Node tree;
        private bool treeDrawn = false;
        private int flashCycleStep;
        //private int activeTrees = 0;

        public static readonly StyledProperty<string> TreeStructureProperty =
           AvaloniaProperty.Register<TreeDiagram, string>(nameof(TreeStructure));

        public string TreeStructure
        {
            get => GetValue(TreeStructureProperty);
            set => SetValue(TreeStructureProperty, value);
        }


        public TreeDiagram()
        {
            Console.WriteLine($"MyControl created: {GetHashCode()}");

           
        }

        private Node buildTree()
        {
            Console.WriteLine("building tree!");
            if (TreeStructure == "one")
            {
                Node t = new Node(13);
                t.AddChildren(new Node(7), new Node(6));
                t.GetChildAt(0).AddChildren(new Node(3), new Node(4));
                t.GetChildAt(1).AddChildren(new Node(3), new Node(3));
                t.GetChildAt(0).GetChildAt(0).AddChildren(new Node(1), new Node(1), new Node(1));
                t.GetChildAt(0).GetChildAt(1).AddChildren(new Node(1), new Node(1), new Node(1), new Node(1));
                t.GetChildAt(1).GetChildAt(0).AddChildren(new Node(1), new Node(1), new Node(1));
                t.GetChildAt(1).GetChildAt(1).AddChildren(new Node(1), new Node(1), new Node(1));
                return t;
            }
            if (TreeStructure == "two")
            {
                Node t = new Node(12);
                t.AddChildren(new Node(7), new Node(5));
                t.GetChildAt(0).AddChildren(new Node(3), new Node(4));
                t.GetChildAt(1).AddChildren(new Node(2), new Node(3));
                t.GetChildAt(0).GetChildAt(0).AddChildren(new Node(1), new Node(1), new Node(1));
                t.GetChildAt(0).GetChildAt(1).AddChildren(new Node(1), new Node(1), new Node(1), new Node(1));
                t.GetChildAt(1).GetChildAt(0).AddChildren(new Node(1), new Node(1));
                t.GetChildAt(1).GetChildAt(1).AddChildren(new Node(1), new Node(1), new Node(1));
                return t;
            }
            return null;

        }
        public override void Render(DrawingContext _context)
        {

            context = _context;
            base.Render(context);


            int width = (int)(this.Bounds.Width * 0.9); //maybe this 0.9 should be a slider for people with weird size screens
            int height = (int)(this.Bounds.Height * 0.9);

            if(!treeDrawn)
            {
                tree = buildTree();
            }


            this.tree.draw(width, height, context, this, !treeDrawn, flashCycleStep, 180); //bpm is hardcoded for now at 180
            flashCycleStep = -1; //-1 will mean dont change it

           
            treeDrawn = true;

            //this.AttachedToVisualTree += OnAttachedToVisualTree;
            //this.DetachedFromVisualTree += OnDetachedFromVisualTree;

        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            //when we resize the window we want to redraw
            base.OnPropertyChanged(change);
            if(tree == null)
            {
                return;
            }
            if (change.Property == BoundsProperty)
            {
                treeDrawn = false;
                flashCycleStep = tree.getCurrentFlashStep();
                tree.StopFlashingCycle();
                tree = null;
                InvalidateVisual();
            }
        }

    }

    public class Node
    {
        private List<Node> children;
        private Node parent;
        private int value;
        Pen blackPen;
        private Point thisPoint = new Point();
        private int width, height;
        private int currentFlashStep = 0;
        public Node(int _value)
        {
            value = _value;
            children = new List<Node>();
            parent = this;
            blackPen = new Pen(Brushes.Black);
        }

        public void AddChild(Node child)
        {
            children.Add(child);
            child.setParent(this);
        }

        public void AddChildren(params Node[] _children)
        {
            foreach (Node c in _children)
            {
                AddChild(c);
            }
        }

        public List<Node> GetChildren()
        {
            return children;
        }

        public Node GetChildAt(int index)
        {
            return children[index];
        }

        public Node GetParent()
        {
            return parent;
        }

        public void setParent(Node _parent)
        {
            parent = _parent;
        }

        public int getValue()
        {
            return value;
        }

        public int GetTotalRowSize()
        {
            return TotalRowValUp(0);
        }

        private int TotalRowValUp(int level)
        {
            if(parent == this) //we are at the top level
            {
                return TotalRowValDown(level); //here level is how many levels we need to descend
            }
            else
            {
                return parent.TotalRowValUp(level + 1);
            }
        }

        private int TotalRowValDown(int level) //recursive call that fits into another funciton but can also be called from root node to get the size of the row (level) levels down
        {
            if (level == 0)
            {
                return 1;
            }
            else
            {
                int total = 0;
                foreach (Node c in children)
                {
                    total += c.TotalRowValDown(level - 1);
                }
                return total;
            }
        }

        public int getDepthOfSubtree()  //assumes all branches have the same depth, which they should do
        {
            if (children.Count == 0 )
            {
                return 0;
            }
            else
            {
                return children[0].getDepthOfSubtree() + 1;
            }
        }
        public void draw(int _width, int _height, DrawingContext context, TreeDiagram container, bool firstRun, int flashCycleStep, double bpm)
        {
            //to be called on the root node only
            width = _width;
            height = _height;
            if(flashCycleStep != -1)
                currentFlashStep = flashCycleStep;

            int startX = width / 2;
            int startY = 0;
            int depth = getDepthOfSubtree();
            int[] howfaralong = new int[depth + 1]; //this shit is becoming more overengineered and overcomplicated by the second 

            Point s = new Point(startX, startY);
            setThisPoint(s);

            FormattedText formatted = new FormattedText(Convert.ToString(value), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 20, Brushes.Black);
            context.DrawText(formatted, new Point(startX, startY));

            int endY = (int)(((double)1 / depth) * height);
            for (int i = 0; i < children.Count; i++)
            {
                int endX = (int)( ((i + (int)(children.Count / 2.0)) / (children.Count + 1.0)) * width);
                Point e = new Point(endX, endY);
                context.DrawLine(blackPen, s, e);

                children[i].drawRecursive(context, width, height, depth, endX, endY, ref howfaralong);
                children[i].setThisPoint(e);
                howfaralong[getDepthOfSubtree()] += 1; //not convinced here tbh but seems to work, might want its own loop
            }

            if (firstRun)
            {
                Console.WriteLine("starting flash cycle");
                StartFlashingCycle(container, bpm, context); //start flashing
            }
        }

        //for a row with totalwidth, this node is subtreewidth wide, and is howfaralong in the row
        //so we want to draw the nodes between howfaralong and howfaralong + subtreewidth
        public void drawRecursive(DrawingContext context, int width, int height, int depth, int startX, int startY, ref int[] howfaralong)
        {
            //if howfaralong + subtreewidth > totalwidth then thats bad and we should throw an error
            if (children.Count >= 1)
            {
                int sectionsize = (int)((width) / (children[0].GetTotalRowSize())); 

                int endY = (int)(((depth - getDepthOfSubtree() + 1.0) / depth) * height);
                for (int i = 0; i < children.Count; i++)
                {
                    int endX = (int)((i + howfaralong[getDepthOfSubtree()] + 0.5) * sectionsize);
                    Point s = new Point(startX, startY);
                    Point e = new Point(endX, endY);
                    context.DrawLine(blackPen, s, e);

                    children[i].drawRecursive(context, width, height, depth, endX, endY, ref howfaralong);
                    children[i].setThisPoint(e);

                }
                for (int i = 0; i < children.Count; i++)
                {
                    howfaralong[getDepthOfSubtree()] += 1;
                }
            }
            FormattedText formatted = new FormattedText(Convert.ToString(value), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 20, Brushes.Black);
            context.DrawEllipse(Brushes.White, blackPen, new Point(startX, startY), 12, 12);
            context.DrawText(formatted, new Point(startX - 5, startY - 15));
        }

        private void setThisPoint(Point point)
        {
            thisPoint = point;
        }

        private Point getThisPoint()
        {
            return thisPoint;
        }

        public void setColour(DrawingContext context, Pen pen, TreeDiagram inst)
        {
            //only meant to be called on leaf nodes
            if(parent != this)
            {
                Point p = getThisPoint();
                context.DrawLine(pen, p, parent.getThisPoint());
                parent.setColour(context, pen, inst);

                FormattedText formatted = new FormattedText(Convert.ToString(value), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 20, pen.Brush);
                context.DrawEllipse(Brushes.White, pen, p, 12, 12);
                context.DrawText(formatted, new Point(p.X - 5, p.Y - 15));


            }

        }
        private List<Node> GetBottomRowNodes()
        {
            List<Node> bottomRowNodes = new List<Node>();
            GetBottomRowNodesRecursive(this, bottomRowNodes);
            return bottomRowNodes;
        }
        private void GetBottomRowNodesRecursive(Node node, List<Node> bottomRowNodes)
        {
            if (node.GetChildren().Count == 0)
            {
                bottomRowNodes.Add(node);
            }
            else
            {
                foreach (Node child in node.GetChildren())
                {
                    GetBottomRowNodesRecursive(child, bottomRowNodes);
                }
            }
        }

        private CancellationTokenSource? _flashCts;

        public void StartFlashingCycle(TreeDiagram inst, double bpm, DrawingContext context)
        {
            StopFlashingCycle(); // make sure we don't start twice
            _flashCts = new CancellationTokenSource();
            CancellationToken token = _flashCts.Token;

            _ = FlashCycleAsync(token, inst, bpm, context);
        }
        public void StopFlashingCycle()
        {
            if (_flashCts != null)
            {
                _flashCts.Cancel();
                _flashCts.Dispose();
                _flashCts = null;
            }
        }

        public int getCurrentFlashStep()
        {
            return currentFlashStep;
        }
        private async Task FlashCycleAsync(CancellationToken token, TreeDiagram inst, double bpm, DrawingContext context)
        {
            List<Node> bottomRowNodes = GetBottomRowNodes();
            Console.WriteLine(bottomRowNodes.Count);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(60.0/bpm), token).ConfigureAwait(false); //bpm setter

                    Dispatcher.UIThread.Post(() =>
                    {
                        bottomRowNodes[currentFlashStep].setColour(context, new Pen(Brushes.Red, 3), inst);


                        inst.InvalidateVisual();

                        currentFlashStep = (currentFlashStep + 1) % bottomRowNodes.Count;

                    }, DispatcherPriority.Normal);
                }
            }
            catch (OperationCanceledException)
            {
                // normal cancellation — do nothing
            }
        }

    }
}
