#!/usr/bin/env python
"""
AutoCAD MCP Server (Socket Client Version)
Connects to AutoCAD C# Add-in via TCP localhost:8964
"""
from mcp.server.fastmcp import FastMCP, Context
from typing import Optional, List, Dict, Any
import socket
import json
import logging

# Initialize FastMCP
mcp = FastMCP("AutoCAD-Architect-Server")

HOST = '127.0.0.1'
PORT = 8964

def send_command(command: str, args: Dict[str, Any] = None) -> str:
    """Send command to AutoCAD Add-in via Socket"""
    if args is None:
        args = {}
        
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.settimeout(5.0) # 5 seconds timeout
            try:
                s.connect((HOST, PORT))
            except ConnectionRefusedError:
                return "Error: Could not connect to AutoCAD. Please ensure AutoCAD is running and the MCP Server is started from the 'MCP Tools' ribbon."

            request = {
                "Command": command,
                "Args": args
            }
            msg = json.dumps(request)
            s.sendall(msg.encode('utf-8'))
            
            # Receive response (handle potentially large data)
            chunks = []
            while True:
                chunk = s.recv(8192)
                if not chunk:
                    break
                chunks.append(chunk)
            
            if not chunks:
                return "Error: No data received from AutoCAD."

            data = b"".join(chunks)
            response = json.loads(data.decode('utf-8'))
            
            if response.get("Success"):
                return response.get("Message", "Success")
            else:
                return f"AutoCAD Error: {response.get('Message', 'Unknown error')}"
                
    except json.JSONDecodeError:
        return "Error: Received invalid JSON from AutoCAD. The response might be too large or corrupted."
    except Exception as e:
        return f"Connection Failed: {str(e)}"

# ======= Tools Mapping =======

@mcp.tool()
def create_layer(ctx: Context, name: str, color: int = 7) -> str:
    """Create a new layer with specified color (ACI index)"""
    return send_command("create_layer", {"name": name, "color": color})

@mcp.tool()
def draw_line(ctx: Context, start_x: float, start_y: float, end_x: float, end_y: float, layer: str = "0") -> str:
    """Draw a line segment"""
    return send_command("draw_line", {
        "start_x": start_x, 
        "start_y": start_y, 
        "end_x": end_x, 
        "end_y": end_y,
        "layer": layer
    })

@mcp.tool()
def draw_wall(ctx: Context, start_x: float, start_y: float, end_x: float, end_y: float, width: float = 200) -> str:
    """Draw a architectural wall (double line)"""
    return send_command("draw_wall", {
        "start_x": start_x,
        "start_y": start_y,
        "end_x": end_x,
        "end_y": end_y,
        "width": width
    })

@mcp.tool()
def get_layers(ctx: Context) -> str:
    """List all layers in the current drawing"""
    return send_command("get_layers")

@mcp.tool()
def find_overlaps(ctx: Context, layer: Optional[str] = None) -> str:
    """Find overlapping lines in the drawing (or specified layer)
    
    Args:
        layer: Optional layer name to filter
    """
    return send_command("find_overlaps", {"layer": layer} if layer else {})

@mcp.tool()
def clean_overlaps(ctx: Context, layer: Optional[str] = None) -> str:
    """Delete shorter overlapping line segments
    
    Args:
        layer: Optional layer name to filter
    """
    return send_command("clean_overlaps", {"layer": layer} if layer else {})

@mcp.tool()
def connect_lines(ctx: Context, layer: Optional[str] = None, tolerance: float = 10.0) -> str:
    """Ensure lines on the same layer are connected by snapping nearby endpoints
    
    Args:
        layer: Optional layer name to filter
        tolerance: Max distance to snap (default 10.0 mm)
    """
    return send_command("connect_lines", {"layer": layer, "tolerance": tolerance} if layer else {"tolerance": tolerance})

@mcp.tool()
def get_blocks_in_view(ctx: Context) -> str:
    """Get a list of all blocks visible in the current view with their counts and descriptions"""
    return send_command("get_blocks_in_view")

@mcp.tool()
def rename_block(ctx: Context, old_name: str, new_name: str) -> str:
    """Rename a block definition
    
    Args:
        old_name: Current block name
        new_name: New block name
    """
    return send_command("rename_block", {"old_name": old_name, "new_name": new_name})

@mcp.tool()
def update_block_description(ctx: Context, name: str, description: str) -> str:
    """Update the description (comments) of a block definition
    
    Args:
        name: Block name
        description: New description text
    """
    # Input validation
    if not name or len(name) > 255:
        return "Error: Invalid block name"
    if len(description) > 2000:
        return "Error: Description too long (max 2000 characters)"
    return send_command("update_block_description", {"name": name, "description": description})

@mcp.tool()
def create_new_drawing(ctx: Context) -> str:
    """Create a new empty drawing in AutoCAD"""
    return send_command("create_new_drawing")

@mcp.tool()
def draw_circle(ctx: Context, center_x: float, center_y: float, radius: float, layer: str = "0") -> str:
    """Draw a circle with specified center point and radius
    
    Args:
        center_x: X coordinate of circle center
        center_y: Y coordinate of circle center
        radius: Circle radius (must be > 0)
        layer: Optional layer name (default: "0")
    """
    # Input validation
    if radius <= 0:
        return "Error: Radius must be greater than 0"
    if abs(center_x) > 1e10 or abs(center_y) > 1e10 or radius > 1e10:
        return "Error: Values out of acceptable range"
    
    return send_command("draw_circle", {
        "center_x": center_x,
        "center_y": center_y,
        "radius": radius,
        "layer": layer
    })

@mcp.tool()
def set_layer_color(ctx: Context, layer: str, color: int) -> str:
    """Set the color of an existing layer
    
    Args:
        layer: Layer name to modify
        color: AutoCAD Color Index (ACI) 0-256
    """
    # Input validation
    if not layer or len(layer) > 255:
        return "Error: Invalid layer name"
    if color < 0 or color > 256:
        return "Error: Color index must be between 0 and 256"
    
    return send_command("set_layer_color", {"layer": layer, "color": color})

if __name__ == "__main__":
    print("🏗️ AutoCAD MCP Server (建築師版) 啟動中...")
    print("📋 可用工具：")
    print("   繪圖工具：draw_line, draw_circle, draw_wall")
    print("   圖層管理：create_layer, get_layers, set_layer_color")
    print("   圖塊工具：get_blocks_in_view, rename_block, update_block_description")
    print("   圖面整理：find_overlaps, clean_overlaps, connect_lines")
    print("   其他工具：create_new_drawing")
    mcp.run()

