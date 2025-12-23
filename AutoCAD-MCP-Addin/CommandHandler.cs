using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.EditorInput;
using Newtonsoft.Json;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AutoCADMCP.Server
{
    public static class CommandHandler
    {
        public static string Execute(Document doc, string command, Dictionary<string, object> args)
        {
            if (args == null) args = new Dictionary<string, object>();
            
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
                case "connect_lines":
                    return ConnectLines(doc, args);
                case "get_blocks_in_view":
                    return GetBlocksInView(doc);
                case "rename_block":
                    return RenameBlock(doc, args);
                case "update_block_description":
                    return UpdateBlockDescription(doc, args);
                case "create_new_drawing":
                    return CreateNewDrawing(doc);
                case "draw_circle":
                    return DrawCircle(doc, args);
                case "set_layer_color":
                    return SetLayerColor(doc, args);
                case "change_color":
                    return ChangeColor(doc, args);
                case "get_coordinate_info":
                    return GetCoordinateInfo(doc);
                case "get_drawing_extents":
                    return GetDrawingExtents(doc);
                default:
                    return $"Unknown command: {command}";
            }
        }

        private static string GetBlocksInView(Document doc)
        {
            var editor = doc.Editor;
            var view = editor.GetCurrentView();
            
            // Calculate View Extents
            double height = view.Height;
            double width = view.Width;
            Point3d center = new Point3d(view.CenterPoint.X, view.CenterPoint.Y, 0);
            
            Point3d min = new Point3d(center.X - width/2, center.Y - height/2, 0);
            Point3d max = new Point3d(center.X + width/2, center.Y + height/2, 0);

            // Select Crossing Window
            PromptSelectionResult res = editor.SelectCrossingWindow(min, max);
            
            if (res.Status != PromptStatus.OK)
                return "No objects found in current view.";

            Dictionary<string, int> blockCounts = new Dictionary<string, int>();
            Dictionary<string, string> blockDescs = new Dictionary<string, string>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

                foreach (ObjectId id in res.Value.GetObjectIds())
                {
                    if (id.ObjectClass.DxfName == "INSERT")
                    {
                        BlockReference blkRef = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                        // Get effective name for dynamic blocks
                        string name = blkRef.Name;
                        if (blkRef.IsDynamicBlock)
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead);
                            name = btr.Name;
                        }

                        if (!blockCounts.ContainsKey(name))
                        {
                            blockCounts[name] = 0;
                            // Get description
                            if (bt.Has(name))
                            {
                                BlockTableRecord def = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);
                                blockDescs[name] = def.Comments ?? "";
                            }
                        }
                        blockCounts[name]++;
                    }
                }
                tr.Commit();
            }

            if (blockCounts.Count == 0) return "No blocks found in current view.";

            List<object> result = new List<object>();
            foreach (var kvp in blockCounts)
            {
                result.Add(new { 
                    Name = kvp.Key, 
                    Count = kvp.Value, 
                    Description = blockDescs.ContainsKey(kvp.Key) ? blockDescs[kvp.Key] : "" 
                });
            }

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        private static string RenameBlock(Document doc, Dictionary<string, object> args)
        {
            try
            {
                if (!args.ContainsKey("old_name") || !args.ContainsKey("new_name"))
                    return "Error: Missing block names.";

                string oldName = args["old_name"].ToString();
                string newName = args["new_name"].ToString();

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForWrite);
                    
                    if (!bt.Has(oldName)) return $"Error: Block '{oldName}' not found.";
                    if (bt.Has(newName)) return $"Error: Block '{newName}' already exists.";

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[oldName], OpenMode.ForWrite);
                    btr.Name = newName;
                    
                    tr.Commit();
                    return $"Successfully renamed block '{oldName}' to '{newName}'.";
                }
            }
            catch (Exception ex) { return $"Error renaming block: {ex.Message}"; }
        }

        private static string UpdateBlockDescription(Document doc, Dictionary<string, object> args)
        {
            try
            {
                if (!args.ContainsKey("name") || !args.ContainsKey("description"))
                    return "Error: Missing parameters.";

                string name = args["name"].ToString();
                string desc = args["description"].ToString();

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    
                    if (!bt.Has(name)) return $"Error: Block '{name}' not found.";

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForWrite);
                    btr.Comments = desc;
                    
                    tr.Commit();
                    return $"Updated description for block '{name}'.";
                }
            }
            catch (Exception ex) { return $"Error updating block description: {ex.Message}"; }
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
                
                // PERFORMANCE GUARD: Limit N^2 calculation
                if (lines.Count > 5000)
                {
                    return $"Error: Too many lines detected ({lines.Count}). For performance reasons, overlap detection is limited to 5000 lines. Please filter by layer.";
                }

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
                
                if (lines.Count > 5000)
                {
                    return "Error: Too many lines to process cleanup safely. Please filter by layer.";
                }

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
            // Check for degenerate lines (zero length)
            if (l1.Length < Tolerance.Global.EqualPoint || l2.Length < Tolerance.Global.EqualPoint)
                return false;

            // 1. Check if parallel (Cross product of direction vectors ~ 0)
            Vector3d v1 = l1.EndPoint - l1.StartPoint;
            Vector3d v2 = l2.EndPoint - l2.StartPoint;
            
            if (!v1.IsParallelTo(v2, Tolerance.Global)) return false;

            // 2. Check if collinear (Start point of l2 lies on infinite line of l1)
            Line3d line1 = new Line3d(l1.StartPoint, l1.EndPoint);
            if (!line1.IsOn(l2.StartPoint, Tolerance.Global)) return false;

            // 3. Check for interval overlap
            // Project all points onto the line direction
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
            try
            {
                if (!args.ContainsKey("name")) return "Error: Missing layer name.";
                string name = args["name"].ToString();
                short colorIndex = args.ContainsKey("color") ? Convert.ToInt16(args["color"]) : (short)7;

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
            catch (Exception ex) { return $"Error creating layer: {ex.Message}"; }
        }

        private static string DrawLine(Document doc, Dictionary<string, object> args)
        {
            try
            {
                if (!args.ContainsKey("start_x") || !args.ContainsKey("start_y") || !args.ContainsKey("end_x") || !args.ContainsKey("end_y"))
                    return "Error: Missing coordinates.";

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
            catch (Exception ex) { return $"Error drawing line: {ex.Message}"; }
        }

        private static string DrawWall(Document doc, Dictionary<string, object> args)
        {
            try
            {
                if (!args.ContainsKey("start_x") || !args.ContainsKey("start_y") || !args.ContainsKey("end_x") || !args.ContainsKey("end_y") || !args.ContainsKey("width"))
                    return "Error: Missing wall parameters.";

                double x1 = Convert.ToDouble(args["start_x"]);
                double y1 = Convert.ToDouble(args["start_y"]);
                double x2 = Convert.ToDouble(args["end_x"]);
                double y2 = Convert.ToDouble(args["end_y"]);
                double width = Convert.ToDouble(args["width"]); 
                
                Vector3d dir = new Point3d(x2, y2, 0) - new Point3d(x1, y1, 0);
                if (dir.Length < Tolerance.Global.EqualPoint) return "Error: Wall length is zero.";
                
                Vector3d perp = dir.GetPerpendicularVector().GetNormal() * (width / 2.0);

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    string layer = "A-WALL";
                    LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(layer))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layer;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        lt.UpgradeOpen();
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    Line l1 = new Line(new Point3d(x1, y1, 0) + perp, new Point3d(x2, y2, 0) + perp);
                    Line l2 = new Line(new Point3d(x1, y1, 0) - perp, new Point3d(x2, y2, 0) - perp);
                    l1.Layer = layer; l2.Layer = layer;

                    btr.AppendEntity(l1); btr.AppendEntity(l2);
                    tr.AddNewlyCreatedDBObject(l1, true); tr.AddNewlyCreatedDBObject(l2, true);
                    
                    tr.Commit();
                    return $"Drawn wall from ({x1},{y1}) to ({x2},{y2}) width {width}";
                }
            }
            catch (Exception ex) { return $"Error drawing wall: {ex.Message}"; }
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

        private static string CreateNewDrawing(Document doc)
        {
            try
            {
                // Create a new drawing using DocumentCollectionExtension
                DocumentCollection docMgr = Application.DocumentManager;
                Document newDoc = docMgr.Add("");
                
                if (newDoc != null)
                {
                    return $"Created new drawing: {newDoc.Name}";
                }
                return "Failed to create new drawing.";
            }
            catch (Exception ex)
            {
                return $"Error creating new drawing: {ex.Message}";
            }
        }

        private static string DrawCircle(Document doc, Dictionary<string, object> args)
        {
            try
            {
                // Input validation
                if (!args.ContainsKey("center_x") || !args.ContainsKey("center_y") || !args.ContainsKey("radius"))
                {
                    return "Error: Missing required parameters (center_x, center_y, radius)";
                }

                double cx = Convert.ToDouble(args["center_x"]);
                double cy = Convert.ToDouble(args["center_y"]);
                double radius = Convert.ToDouble(args["radius"]);
                string layer = args.ContainsKey("layer") ? args["layer"].ToString() : "0";

                // Validate radius
                if (radius <= 0)
                {
                    return "Error: Radius must be greater than 0";
                }

                // Validate reasonable bounds (prevent extremely large values)
                if (Math.Abs(cx) > 1e10 || Math.Abs(cy) > 1e10 || radius > 1e10)
                {
                    return "Error: Coordinate or radius values are out of acceptable range";
                }

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    Circle circle = new Circle(new Point3d(cx, cy, 0), Vector3d.ZAxis, radius);
                    circle.Layer = layer;

                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    tr.Commit();

                    return $"Drawn circle at ({cx},{cy}) with radius {radius} on layer {layer}";
                }
            }
            catch (Exception ex)
            {
                return $"Error drawing circle: {ex.Message}";
            }
        }

        private static string SetLayerColor(Document doc, Dictionary<string, object> args)
        {
            try
            {
                // Input validation
                if (!args.ContainsKey("layer") || !args.ContainsKey("color"))
                {
                    return "Error: Missing required parameters (layer, color)";
                }

                string layerName = args["layer"].ToString();
                short colorIndex = Convert.ToInt16(args["color"]);

                // Validate color index (ACI: 0-256)
                if (colorIndex < 0 || colorIndex > 256)
                {
                    return "Error: Color index must be between 0 and 256";
                }

                // Validate layer name (prevent injection)
                if (string.IsNullOrWhiteSpace(layerName) || layerName.Length > 255)
                {
                    return "Error: Invalid layer name";
                }

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

                    if (!lt.Has(layerName))
                    {
                        return $"Layer '{layerName}' not found.";
                    }

                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);

                    tr.Commit();
                    return $"Set layer '{layerName}' color to {colorIndex}";
                }
            }
            catch (Exception ex)
            {
                return $"Error setting layer color: {ex.Message}";
            }
        }

        private static string ConnectLines(Document doc, Dictionary<string, object> args)
        {
            string layerFilter = args.ContainsKey("layer") ? args["layer"].ToString() : null;
            double tolerance = args.ContainsKey("tolerance") ? Convert.ToDouble(args["tolerance"]) : 10.0;
            int connectCount = 0;

            try
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    List<Line> lines = GetAllLines(tr, btr, layerFilter);
                    if (lines.Count > 5000) return "Error: Too many lines to process. Please filter by layer.";

                    for (int i = 0; i < lines.Count; i++)
                    {
                        for (int j = i + 1; j < lines.Count; j++)
                        {
                            Line l1 = lines[i];
                            Line l2 = lines[j];

                            // Try to connect 4 endpoint combinations
                            if (TrySnap(l1, l2, (p1, p2) => { l1.UpgradeOpen(); l1.StartPoint = p2; }, tolerance)) connectCount++;
                            else if (TrySnap(l1, l2, (p1, p2) => { l1.UpgradeOpen(); l1.EndPoint = p2; }, tolerance)) connectCount++;
                            else if (TrySnap(l2, l1, (p1, p2) => { l2.UpgradeOpen(); l2.StartPoint = p2; }, tolerance)) connectCount++;
                            else if (TrySnap(l2, l1, (p1, p2) => { l2.UpgradeOpen(); l2.EndPoint = p2; }, tolerance)) connectCount++;
                        }
                    }
                    tr.Commit();
                }
                return $"Successfully connected {connectCount} line endpoints within {tolerance}mm tolerance.";
            }
            catch (Exception ex)
            {
                return $"Error connecting lines: {ex.Message}";
            }
        }

        private static bool TrySnap(Line moveLine, Line stayLine, Action<Point3d, Point3d> updateAction, double tol)
        {
            Point3d[] p1s = { moveLine.StartPoint, moveLine.EndPoint };
            Point3d[] p2s = { stayLine.StartPoint, stayLine.EndPoint };

            foreach (var p1 in p1s)
            {
                foreach (var p2 in p2s)
                {
                    if (p1.DistanceTo(p2) > 0 && p1.DistanceTo(p2) <= tol)
                    {
                        updateAction(p1, p2);
                        return true;
                    }
                }
            }
            return false;
        }

        private static string ChangeColor(Document doc, Dictionary<string, object> args)
        {
            try
            {
                if (!args.ContainsKey("from_color") || !args.ContainsKey("to_color"))
                    return "Error: Missing color parameters.";

                short fromColor = Convert.ToInt16(args["from_color"]);
                short toColor = Convert.ToInt16(args["to_color"]);
                int entityCount = 0;
                int layerCount = 0;

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    // 1. Change Layer Colors (Skip locked ones)
                    LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (ltr.Color.ColorIndex == fromColor)
                        {
                            if (!ltr.IsLocked)
                            {
                                ltr.UpgradeOpen();
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, toColor);
                                layerCount++;
                            }
                        }
                    }

                    // 2. Change Entity Colors in all blocks (ModelSpace, PaperSpace, and Block Definitions)
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        
                        foreach (ObjectId id in btr)
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            // Check if entity has color explicitly set to fromColor (not ByLayer)
                            if (ent.Color.ColorIndex == fromColor && !ent.Color.IsByLayer)
                            {
                                // Check if layer is locked before modifying
                                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                                if (!layer.IsLocked)
                                {
                                    ent.UpgradeOpen();
                                    ent.Color = Color.FromColorIndex(ColorMethod.ByAci, toColor);
                                    entityCount++;
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
                return $"Changed {layerCount} layers and {entityCount} entities from color {fromColor} to {toColor}.";
            }
            catch (Exception ex) { return $"Error changing color: {ex.Message}"; }
        }

        private static string GetCoordinateInfo(Document doc)
        {
            try
            {
                var db = doc.Database;
                var ed = doc.Editor;
                
                // Get current UCS
                var ucs = ed.CurrentUserCoordinateSystem;
                Point3d ucsOrigin = ucs.CoordinateSystem3d.Origin;
                string ucsName = "Unnamed";
                
                // Try to get UCS name
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    UcsTable ut = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
                    foreach (ObjectId id in ut)
                    {
                        UcsTableRecord utr = (UcsTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (utr.Origin == ucsOrigin)
                        {
                            ucsName = utr.Name;
                            break;
                        }
                    }
                    tr.Commit();
                }

                // Get drawing extents
                Extents3d extents = new Extents3d();
                bool hasExtents = false;
                
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    
                    foreach (ObjectId id in btr)
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                        if (ent.Bounds.HasValue)
                        {
                            if (!hasExtents)
                            {
                                extents = ent.Bounds.Value;
                                hasExtents = true;
                            }
                            else
                            {
                                extents.AddExtents(ent.Bounds.Value);
                            }
                        }
                    }
                    tr.Commit();
                }

                if (!hasExtents)
                    return "No geometry found in model space.";

                // Calculate center and distance from WCS origin
                Point3d center = new Point3d(
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                    (extents.MinPoint.Z + extents.MaxPoint.Z) / 2
                );
                double distanceFromOrigin = center.DistanceTo(Point3d.Origin);

                // Build report
                var report = new
                {
                    WCS_Origin = "0, 0, 0 (Fixed)",
                    UCS_Name = ucsName,
                    UCS_Origin = $"{ucsOrigin.X:F2}, {ucsOrigin.Y:F2}, {ucsOrigin.Z:F2}",
                    UCS_Is_World = ucsOrigin.DistanceTo(Point3d.Origin) < 0.001,
                    Drawing_Min = $"{extents.MinPoint.X:F2}, {extents.MinPoint.Y:F2}, {extents.MinPoint.Z:F2}",
                    Drawing_Max = $"{extents.MaxPoint.X:F2}, {extents.MaxPoint.Y:F2}, {extents.MaxPoint.Z:F2}",
                    Drawing_Center = $"{center.X:F2}, {center.Y:F2}, {center.Z:F2}",
                    Distance_From_WCS_Origin = $"{distanceFromOrigin:F2} units",
                    Revit_Import_Recommendation = distanceFromOrigin < 30000 
                        ? "Safe for 'Origin to Origin' import." 
                        : "Warning: Drawing is far from origin. Consider moving to (0,0) or use 'Shared Coordinates' in Revit."
                };

                return JsonConvert.SerializeObject(report, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error getting coordinate info: {ex.Message}"; }
        }

        private static string GetDrawingExtents(Document doc)
        {
            try
            {
                var db = doc.Database;
                Extents3d extents = new Extents3d();
                bool hasExtents = false;

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId id in btr)
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                        if (ent.Bounds.HasValue)
                        {
                            if (!hasExtents)
                            {
                                extents = ent.Bounds.Value;
                                hasExtents = true;
                            }
                            else
                            {
                                extents.AddExtents(ent.Bounds.Value);
                            }
                        }
                    }
                    tr.Commit();
                }

                if (!hasExtents)
                    return "No geometry found.";

                return $"Min: ({extents.MinPoint.X:F2}, {extents.MinPoint.Y:F2}, {extents.MinPoint.Z:F2}), Max: ({extents.MaxPoint.X:F2}, {extents.MaxPoint.Y:F2}, {extents.MaxPoint.Z:F2})";
            }
            catch (Exception ex) { return $"Error getting extents: {ex.Message}"; }
        }
    }
}

