using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;

namespace AutoCADMCP.Server
{
    public static class CommandHandler
    {
        public static string Execute(Document doc, string command, Dictionary<string, object> args)
        {
            switch (command)
            {
                case "create_layer":
                    return CreateLayer(doc, args);
                case "draw_line":
                    return DrawLine(doc, args);
                case "draw_wall":
                    return DrawWall(doc, args);
                case "get_layers":
                    return GetLayers(doc);
                case "find_overlaps":
                    return FindOverlaps(doc, args);
                case "clean_overlaps":
                    return CleanOverlaps(doc, args);
                default:
                    return $"Unknown command: {command}";
            }
        }

        private static string FindOverlaps(Document doc, Dictionary<string, object> args)
        {
            string layerFilter = args.ContainsKey("layer") ? args["layer"].ToString() : null;
            int count = 0;
            List<string> details = new List<string>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                List<Line> lines = GetAllLines(tr, btr, layerFilter);

                for (int i = 0; i < lines.Count; i++)
                {
                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        if (AreLinesOverlapping(lines[i], lines[j]))
                        {
                            count++;
                            if (count <= 5) details.Add($"Overlap: Line {lines[i].Handle} & {lines[j].Handle}");
                        }
                    }
                }
                tr.Commit();
            }

            string detailStr = count > 5 ? $"\nExamples:\n{string.Join("\n", details)}..." : $"\n{string.Join("\n", details)}";
            return $"Found {count} overlapping line pairs.{detailStr}";
        }

        private static string CleanOverlaps(Document doc, Dictionary<string, object> args)
        {
            string layerFilter = args.ContainsKey("layer") ? args["layer"].ToString() : null;
            int deletedCount = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                List<Line> lines = GetAllLines(tr, btr, layerFilter);
                HashSet<ObjectId> toDelete = new HashSet<ObjectId>();

                for (int i = 0; i < lines.Count; i++)
                {
                    if (toDelete.Contains(lines[i].ObjectId)) continue;

                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        if (toDelete.Contains(lines[j].ObjectId)) continue;

                        if (AreLinesOverlapping(lines[i], lines[j]))
                        {
                            // Strategy: Delete the shorter one
                            if (lines[i].Length < lines[j].Length)
                            {
                                toDelete.Add(lines[i].ObjectId);
                                break; // Line i is deleted, move to next i
                            }
                            else
                            {
                                toDelete.Add(lines[j].ObjectId);
                            }
                        }
                    }
                }

                foreach (ObjectId id in toDelete)
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                    obj.Erase();
                    deletedCount++;
                }

                tr.Commit();
            }
            return $"Cleaned up {deletedCount} short overlapping segments.";
        }

        // Geometric Helpers
        private static List<Line> GetAllLines(Transaction tr, BlockTableRecord btr, string layer)
        {
            List<Line> lines = new List<Line>();
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.DxfName == "LINE")
                {
                    Line l = (Line)tr.GetObject(id, OpenMode.ForRead);
                    if (layer == null || l.Layer == layer)
                    {
                        lines.Add(l);
                    }
                }
            }
            return lines;
        }

        private static bool AreLinesOverlapping(Line l1, Line l2)
        {
            // 1. Check if parallel (Cross product of direction vectors ~ 0)
            Vector3d v1 = l1.EndPoint - l1.StartPoint;
            Vector3d v2 = l2.EndPoint - l2.StartPoint;
            
            if (!v1.IsParallelTo(v2, Tolerance.Global)) return false;

            // 2. Check if collinear (Start point of l2 lies on infinite line of l1)
            Line3d line1 = new Line3d(l1.StartPoint, l1.EndPoint);
            if (!line1.IsOn(l2.StartPoint, Tolerance.Global)) return false;

            // 3. Check for interval overlap
            // Project all points onto the line direction
            double t1_start = 0;
            double t1_end = v1.Length;
            
            Vector3d dir = v1.GetNormal();
            double t2_start = (l2.StartPoint - l1.StartPoint).DotProduct(dir);
            double t2_end = (l2.EndPoint - l1.StartPoint).DotProduct(dir);

            double min2 = Math.Min(t2_start, t2_end);
            double max2 = Math.Max(t2_start, t2_end);

            // Overlap condition: intervals [0, len1] and [min2, max2] intersect
            return Math.Max(0, min2) < Math.Min(t1_end, max2) - Tolerance.Global.EqualPoint;
        }

        private static string CreateLayer(Document doc, Dictionary<string, object> args)
        {
            string name = args["name"].ToString();
            short colorIndex = Convert.ToInt16(args["color"]);

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                
                if (!lt.Has(name))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = name;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    
                    lt.UpgradeOpen();
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                    tr.Commit();
                    return $"Created layer '{name}' with color {colorIndex}";
                }
                return $"Layer '{name}' already exists";
            }
        }

        private static string DrawLine(Document doc, Dictionary<string, object> args)
        {
            double x1 = Convert.ToDouble(args["start_x"]);
            double y1 = Convert.ToDouble(args["start_y"]);
            double x2 = Convert.ToDouble(args["end_x"]);
            double y2 = Convert.ToDouble(args["end_y"]);
            string layer = args.ContainsKey("layer") ? args["layer"].ToString() : "0";

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                Line line = new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0));
                line.Layer = layer;

                btr.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                tr.Commit();
                
                return $"Drawn line from ({x1},{y1}) to ({x2},{y2}) on layer {layer}";
            }
        }

        private static string DrawWall(Document doc, Dictionary<string, object> args)
        {
            // Simplified wall drawing (double line)
            double x1 = Convert.ToDouble(args["start_x"]);
            double y1 = Convert.ToDouble(args["start_y"]);
            double x2 = Convert.ToDouble(args["end_x"]);
            double y2 = Convert.ToDouble(args["end_y"]);
            double width = Convert.ToDouble(args["width"]); // Wall width
            
            // Calculate offset vector
            Vector3d dir = new Point3d(x2, y2, 0) - new Point3d(x1, y1, 0);
            Vector3d perp = dir.GetPerpendicularVector().GetNormal() * (width / 2.0);

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Ensure layer exists
                string layer = "A-WALL";
                CreateLayer(doc, new Dictionary<string, object> { { "name", layer }, { "color", 1 } }); // Red

                Line l1 = new Line(new Point3d(x1, y1, 0) + perp, new Point3d(x2, y2, 0) + perp);
                Line l2 = new Line(new Point3d(x1, y1, 0) - perp, new Point3d(x2, y2, 0) - perp);
                
                l1.Layer = layer;
                l2.Layer = layer;

                btr.AppendEntity(l1);
                btr.AppendEntity(l2);
                tr.AddNewlyCreatedDBObject(l1, true);
                tr.AddNewlyCreatedDBObject(l2, true);
                
                tr.Commit();
                return $"Drawn wall from ({x1},{y1}) to ({x2},{y2}) width {width}";
            }
        }

        private static string GetLayers(Document doc)
        {
            List<string> layers = new List<string>();
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    layers.Add($"{ltr.Name} (Color: {ltr.Color.ColorIndex})");
                }
            }
            return string.Join(", ", layers);
        }
    }
}
