#!/usr/bin/env python3
"""
Cinema Seat Reservation UI (Tkinter) — robust version.

Usage:
  python ui/ui.py [path-to-logic-exe-or-dll]

If no path is provided, the UI prefers:
  CinemaLogic/bin/Debug/net8.0/win-x64/CinemaLogic.dll
then:
  CinemaLogic/bin/Debug/net8.0/win-x64/publish/CinemaLogic.exe
"""

import sys
import json
import subprocess
import tkinter as tk
from tkinter import messagebox, simpledialog
from pathlib import Path

# ---------- Config ----------
DEFAULT_FSHARP_DLL = Path(__file__).resolve().parents[1] / "CinemaLogic" / "bin" / "Debug" / "net8.0" / "win-x64" / "CinemaLogic.dll"
DEFAULT_FSHARP_EXE = Path(__file__).resolve().parents[1] / "CinemaLogic" / "bin" / "Debug" / "net8.0" / "win-x64" / "publish" / "CinemaLogic.exe"

DEFAULT_ROWS = 6
DEFAULT_COLS = 10

# ---------- Helper to call logic ----------
def call_logic(path: str, payload: dict, timeout: int = 10) -> dict:
    """
    Robust caller: try multiple invocation methods and both stdin and CLI-arg delivery.
    For each candidate command, first try providing JSON on stdin, then try passing JSON as single CLI argument.
    Returns parsed JSON dict or raises RuntimeError/FileNotFoundError with helpful info.
    """
    from pathlib import Path
    p = Path(path)

    candidates = []

    # If dll is indicated or present, try dotnet approaches
    if str(p).lower().endswith(".dll") or (p.exists() and p.suffix.lower() == ".dll"):
        candidates.append(["dotnet", str(p)])
        candidates.append([r"C:\Program Files\dotnet\dotnet.exe", str(p)])

    # If there's an exe at the path, try it
    if p.exists() and p.suffix.lower() == ".exe":
        candidates.append([str(p)])

    # Last-resort attempts
    if not candidates:
        if str(p).lower().endswith(".dll"):
            candidates.append(["dotnet", str(p)])
            candidates.append([r"C:\Program Files\dotnet\dotnet.exe", str(p)])
        else:
            candidates.append([str(p)])
            candidates.append(["dotnet", str(p)])

    last_err = []
    payload_json = json.dumps(payload)

    # For each candidate, try two modes: stdin then cli-arg
    for cmd in candidates:
        # --- mode 1: stdin ---
        try:
            proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            stdout, stderr = proc.communicate(payload_json, timeout=timeout)
        except FileNotFoundError as fe:
            last_err.append(f"Command not found: {' '.join(cmd)} -> {fe}")
            stdout, stderr = None, None
        except subprocess.TimeoutExpired:
            try:
                proc.kill()
            except:
                pass
            last_err.append(f"Timeout when running: {' '.join(cmd)} (stdin)")
            stdout, stderr = None, None
        except Exception as e:
            last_err.append(f"Failed to start {' '.join(cmd)} (stdin): {e}")
            stdout, stderr = None, None

        if stderr and stderr.strip():
            last_err.append(f"Command: {' '.join(cmd)} (stdin) produced stderr: {stderr.strip()}")

        if stdout and stdout.strip():
            try:
                return json.loads(stdout.strip())
            except Exception as e:
                raise RuntimeError(f"Failed to parse JSON from command {' '.join(cmd)} (stdin): {e}\nRaw stdout: {stdout}\nStderr: {stderr}")

        # --- mode 2: CLI-arg (pass JSON as single arg) ---
        try:
            cmd_with_arg = list(cmd) + [payload_json]
            proc = subprocess.Popen(cmd_with_arg, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            stdout, stderr = proc.communicate(timeout=timeout)
        except FileNotFoundError as fe:
            last_err.append(f"Command not found: {' '.join(cmd_with_arg)} -> {fe}")
            stdout, stderr = None, None
        except subprocess.TimeoutExpired:
            try:
                proc.kill()
            except:
                pass
            last_err.append(f"Timeout when running: {' '.join(cmd_with_arg)} (arg)")
            stdout, stderr = None, None
        except Exception as e:
            last_err.append(f"Failed to start {' '.join(cmd_with_arg)} (arg): {e}")
            stdout, stderr = None, None

        if stderr and stderr.strip():
            last_err.append(f"Command: {' '.join(cmd_with_arg)} (arg) produced stderr: {stderr.strip()}")

        if stdout and stdout.strip():
            try:
                return json.loads(stdout.strip())
            except Exception as e:
                raise RuntimeError(f"Failed to parse JSON from command {' '.join(cmd_with_arg)} (arg): {e}\nRaw stdout: {stdout}\nStderr: {stderr}")

    # nothing worked
    msg = "No valid response from logic program. Attempts:\n" + "\n".join(last_err)
    raise RuntimeError(msg)



# ---------- UI ----------
class CinemaUI(tk.Tk):
    def __init__(self, logic_path: str | None = None):
        super().__init__()
        self.title("Cinema Seat Reservation")
        self.geometry("980x520")
        self.resizable(False, False)

        # Determine logic path
        if logic_path:
            self.logic_path = logic_path
        else:
            if DEFAULT_FSHARP_DLL.exists():
                self.logic_path = str(DEFAULT_FSHARP_DLL)
            elif DEFAULT_FSHARP_EXE.exists():
                self.logic_path = str(DEFAULT_FSHARP_EXE)
            else:
                # fallback relative DLL (different layout)
                alt = Path(__file__).resolve().parents[1] / "CinemaLogic" / "bin" / "Debug" / "net8.0" / "CinemaLogic.dll"
                self.logic_path = str(alt)

        self.rows = DEFAULT_ROWS
        self.cols = DEFAULT_COLS
        self.buttons = {}
        self.selected = set()
        self.reserved_set = set()  # Track which seats are reserved

        self.create_widgets()

        try:
            self.refresh_layout()
        except Exception as e:
            messagebox.showerror("Startup error", f"Failed to load layout:\n{e}\n\nLogic path: {self.logic_path}")

    def create_widgets(self):
        top = tk.Frame(self, padx=10, pady=10)
        top.pack(fill="both", expand=True)

        self.grid_frame = tk.Frame(top)
        self.grid_frame.pack(side="top", pady=6)

        controls = tk.Frame(top, pady=8)
        controls.pack(side="top", fill="x")
        self.reserve_btn = tk.Button(controls, text="Reserve", command=self.on_reserve)
        self.reserve_btn.pack(side="left")
        self.delete_btn = tk.Button(controls, text="Delete Reservations", command=self.on_delete)
        self.delete_btn.pack(side="left", padx=(6, 0))
        self.list_btn = tk.Button(controls, text="List Reservations", command=self.on_list)
        self.list_btn.pack(side="left", padx=(6, 0))
        self.refresh_btn = tk.Button(controls, text="Refresh", command=self.refresh_layout)
        self.refresh_btn.pack(side="left", padx=(6, 0))
        self.clear_btn = tk.Button(controls, text="Clear Selection", command=self.clear_selection)
        self.clear_btn.pack(side="left", padx=(6, 0))

        self.status_label = tk.Label(top, text="", anchor="w")
        self.status_label.pack(fill="x", pady=(6, 0))

    def make_grid(self):
        for widget in self.grid_frame.winfo_children():
            widget.destroy()
        self.buttons.clear()
        for r in range(1, self.rows + 1):
            for c in range(1, self.cols + 1):
                btn = tk.Button(self.grid_frame, text=f"{r}-{c}", width=6, height=1,
                                command=lambda rr=r, cc=c: self.toggle_select(rr, cc))
                btn.grid(row=r, column=c, padx=2, pady=2)
                self.buttons[(r, c)] = btn

    def toggle_select(self, r, c):
        key = (r, c)
        if key in self.selected:
            self.selected.remove(key)
        else:
            # Allow selecting reserved seats for deletion
            self.selected.add(key)
        self.update_buttons()

    def update_buttons(self):
        for (r, c), btn in self.buttons.items():
            if (r, c) in self.selected:
                btn.config(relief="sunken")
            else:
                btn.config(relief="raised")

    def clear_selection(self):
        self.selected.clear()
        self.update_buttons()
        self.set_status("Selection cleared.")

    def set_status(self, text):
        self.status_label.config(text=text)

    def refresh_layout(self):
        payload = {"action": "get_layout", "rows": self.rows, "cols": self.cols}
        try:
            resp = call_logic(self.logic_path, payload)
        except FileNotFoundError as e:
            raise
        except Exception as e:
            # Bubble up to be handled in startup or button actions
            raise

        if resp.get("status") != "ok":
            raise RuntimeError(f"Logic error: {resp.get('message')}")

        layout = resp.get("layout")
        if not layout:
            raise RuntimeError("Invalid layout response")

        self.rows = layout.get("rows", self.rows)
        self.cols = layout.get("cols", self.cols)
        self.make_grid()
        reserved = layout.get("reserved", [])
        self.reserved_set = set((s["row"], s["col"]) for s in reserved)  # Store for delete validation
        for (r, c), btn in self.buttons.items():
            if (r, c) in self.reserved_set:
                btn.config(bg="red", fg="white", state="normal")  # Keep enabled so can be selected for deletion
            else:
                btn.config(bg="SystemButtonFace", fg="black", state="normal")

        self.selected = set()
        self.set_status("Layout refreshed.")

    def on_reserve(self):
        if not self.selected:
            messagebox.showinfo("No selection", "Select one or more seats to reserve.")
            return

        seats_dto = [{"row": r, "col": c} for (r, c) in sorted(self.selected)]
        client = simpledialog.askstring("Client name (optional)", "Client name (optional):", parent=self)
        payload = {"action": "reserve", "seats": seats_dto, "client": client}

        try:
            resp = call_logic(self.logic_path, payload)
        except FileNotFoundError as e:
            messagebox.showerror("Logic not found", str(e))
            return
        except Exception as e:
            messagebox.showerror("Reservation error", f"Failed to reserve seats:\n{e}")
            return

        status = resp.get("status")
        if status == "ok":
            ticketIds = resp.get("ticketIds", [])
            msg = "Reservation successful.\nTickets:\n" + "\n".join(ticketIds)
            messagebox.showinfo("Reserved", msg)
            try:
                self.refresh_layout()
            except Exception as e:
                messagebox.showwarning("Warning", f"Reserved but failed to refresh layout:\n{e}")
        else:
            msg = resp.get("message", "Reservation failed.")
            failed = resp.get("failed", [])
            if failed:
                failed_text = ", ".join(f"{s['row']}-{s['col']}" for s in failed)
                msg += f"\nFailed seats: {failed_text}"
            messagebox.showwarning("Reservation failed", msg)
            try:
                self.refresh_layout()
            except:
                pass

    def on_delete(self):
        if not self.selected:
            messagebox.showinfo("No selection", "Select one or more RESERVED seats to delete their reservations.")
            return

        # Filter to only reserved seats
        reserved_to_delete = [(r, c) for (r, c) in self.selected if (r, c) in getattr(self, 'reserved_set', set())]
        unreserved_selected = [(r, c) for (r, c) in self.selected if (r, c) not in getattr(self, 'reserved_set', set())]
        
        if not reserved_to_delete:
            if unreserved_selected:
                unreserved_list = ", ".join(f"{r}-{c}" for (r, c) in sorted(unreserved_selected))
                messagebox.showwarning(
                    "No Reserved Seats Selected",
                    f"The following seats are not reserved and cannot be deleted:\n\n{unreserved_list}\n\nPlease select RESERVED (red) seats to delete."
                )
            else:
                messagebox.showinfo("No reserved seats", "Please select RESERVED (red) seats to delete their reservations.")
            return
        
        if unreserved_selected:
            unreserved_list = ", ".join(f"{r}-{c}" for (r, c) in sorted(unreserved_selected))
            messagebox.showwarning(
                "Some Seats Not Reserved",
                f"The following seats are not reserved and will be skipped:\n\n{unreserved_list}\n\nOnly reserved seats will be deleted."
            )

        seats_dto = [{"row": r, "col": c} for (r, c) in sorted(reserved_to_delete)]
        seat_list = ", ".join(f"{r}-{c}" for (r, c) in sorted(reserved_to_delete))
        
        # Confirm deletion
        confirm = messagebox.askyesno(
            "Confirm Deletion",
            f"Are you sure you want to delete reservations for the following RESERVED seats?\n\n{seat_list}\n\nThis action cannot be undone.",
            icon="warning"
        )
        
        if not confirm:
            return

        payload = {"action": "delete_reservations", "seats": seats_dto}

        try:
            resp = call_logic(self.logic_path, payload)
        except FileNotFoundError as e:
            messagebox.showerror("Logic not found", str(e))
            return
        except Exception as e:
            messagebox.showerror("Deletion error", f"Failed to delete reservations:\n{e}")
            return

        status = resp.get("status")
        if status == "ok":
            msg = resp.get("message", "Reservations deleted successfully.")
            messagebox.showinfo("Deleted", msg)
            try:
                self.refresh_layout()
            except Exception as e:
                messagebox.showwarning("Warning", f"Deleted but failed to refresh layout:\n{e}")
        else:
            msg = resp.get("message", "Deletion failed.")
            messagebox.showerror("Deletion failed", msg)
            try:
                self.refresh_layout()
            except:
                pass

    def on_list(self):
        payload = {"action": "list_reservations"}

        try:
            resp = call_logic(self.logic_path, payload)
        except FileNotFoundError as e:
            messagebox.showerror("Logic not found", str(e))
            return
        except Exception as e:
            messagebox.showerror("List error", f"Failed to list reservations:\n{e}")
            return

        status = resp.get("status")
        if status == "ok":
            reservations = resp.get("reservations", [])
            if not reservations or len(reservations) == 0:
                messagebox.showinfo("Reservations", "No reservations found.")
            else:
                # Create a formatted list
                lines = ["Reservations:\n"]
                for res in reservations:
                    seat = res.get("seat", {})
                    ticket_id = res.get("ticketId", "N/A")
                    client = res.get("client") or "Anonymous"
                    row = seat.get("row", "?")
                    col = seat.get("col", "?")
                    lines.append(f"Seat {row}-{col}: Ticket {ticket_id}, Client: {client}")
                
                msg = "\n".join(lines)
                # Use a scrolled text window for long lists
                if len(reservations) > 10:
                    # Create a new window with scrollable text
                    list_window = tk.Toplevel(self)
                    list_window.title("All Reservations")
                    list_window.geometry("500x400")
                    
                    text_widget = tk.Text(list_window, wrap=tk.WORD, padx=10, pady=10)
                    scrollbar = tk.Scrollbar(list_window, orient="vertical", command=text_widget.yview)
                    text_widget.configure(yscrollcommand=scrollbar.set)
                    
                    text_widget.insert("1.0", msg)
                    text_widget.config(state="disabled")
                    
                    text_widget.pack(side="left", fill="both", expand=True)
                    scrollbar.pack(side="right", fill="y")
                else:
                    messagebox.showinfo("Reservations", msg)
        else:
            msg = resp.get("message", "Failed to list reservations.")
            messagebox.showerror("List failed", msg)

def main():
    exe_path = sys.argv[1] if len(sys.argv) > 1 else None
    app = CinemaUI(exe_path)
    app.mainloop()

if __name__ == "__main__":
    main()
