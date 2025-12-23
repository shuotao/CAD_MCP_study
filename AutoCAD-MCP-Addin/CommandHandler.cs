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
                default:
                    return $"Unknown command: {command}";
            }
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
