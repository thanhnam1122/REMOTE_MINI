import tkinter as tk
import sys
from client_gui import ClientGUI

def main():
    root = tk.Tk()
    app = ClientGUI(root)
    
    # Clean shutdown handler
    def on_closing():
        if app.client:
            app.client.stop()
        root.destroy()
        sys.exit(0)

    root.protocol("WM_DELETE_WINDOW", on_closing)
    root.mainloop()

if __name__ == "__main__":
    main()
