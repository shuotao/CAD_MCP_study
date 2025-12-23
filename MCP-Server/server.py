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
            
            # Receive response
            data = s.recv(8192)
            response = json.loads(data.decode('utf-8'))
            
            if response.get("Success"):
                return response.get("Message", "Success")
            else:
                return f"AutoCAD Error: {response.get('Message', 'Unknown error')}"
                
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

if __name__ == "__main__":
    print("🏗️ AutoCAD MCP Server (建築師版) 啟動中...")
    print("📋 可用工具：create_new_drawing, draw_line, draw_circle, draw_rectangle,")
    print("           set_layer, list_layers, set_layer_color, scan_all_entities,")
    print("           find_text, highlight_by_layer, create_arch_layers, draw_wall,")
    print("           find_overlaps, clean_overlaps")
    mcp.run()
