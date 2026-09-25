import tkinter as tk
from tkinter import ttk,messagebox,simpledialog
import socket,threading,json,sqlite3,uuid,os,sys
from pathlib import Path
APP="Vitakora"; UDP=50505; TCP=50506
DATA=Path(os.getenv("APPDATA",Path.home()))/"Vitakora"; DATA.mkdir(parents=True,exist_ok=True)
CFG=DATA/"config.json"; DB=DATA/"vitakora.db"
def loadcfg():
    try:return json.loads(CFG.read_text(encoding="utf-8"))
    except:return {}
def savecfg(c):CFG.write_text(json.dumps(c,ensure_ascii=False,indent=2),encoding="utf-8")
def db():
    c=sqlite3.connect(DB);c.execute("create table if not exists messages(id text primary key, sender text, body text, created text default current_timestamp)")
    c.commit();return c
class Server:
    def __init__(self,onmsg):self.onmsg=onmsg;self.run=True
    def start(self):
        self.s=socket.socket();self.s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1);self.s.bind(("",TCP));self.s.listen()
        threading.Thread(target=self.accept,daemon=True).start();threading.Thread(target=self.broadcast,daemon=True).start()
    def accept(self):
        while self.run:
            try:c,a=self.s.accept()
            except:break
            threading.Thread(target=self.client,args=(c,),daemon=True).start()
    def client(self,c):
        f=c.makefile("rwb")
        try:
            for line in f:
                m=json.loads(line);mid=m.get("id") or str(uuid.uuid4());body=m.get("body","");sender=m.get("sender","Puesto")
                with db() as d:d.execute("insert or ignore into messages(id,sender,body) values(?,?,?)",(mid,sender,body));d.commit()
                self.onmsg(sender,body);f.write((json.dumps({"ok":True,"id":mid})+"\n").encode());f.flush()
        except:pass
        finally:c.close()
    def broadcast(self):
        s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);s.setsockopt(socket.SOL_SOCKET,socket.SO_BROADCAST,1)
        while self.run:
            try:s.sendto(json.dumps({"app":"Vitakora","port":TCP}).encode(),("<broadcast>",UDP))
            except:pass
            import time;time.sleep(3)
def discover(sec=3):
    s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1)
    try:s.bind(("",UDP))
    except:return None
    s.settimeout(sec)
    try:
        raw,a=s.recvfrom(4096);m=json.loads(raw)
        return a[0] if m.get("app")=="Vitakora" else None
    except:return None
    finally:s.close()
def send(host,sender,body):
    c=socket.create_connection((host,TCP),3);f=c.makefile("rwb")
    f.write((json.dumps({"sender":sender,"body":body})+"\n").encode());f.flush();r=json.loads(f.readline());c.close();return r
class App(tk.Tk):
    def __init__(self):
        super().__init__();self.title("Vitakora 3.0 Preview");self.geometry("1100x700");self.minsize(850,550)
        self.cfg=loadcfg()
        if not self.cfg:self.first()
        self.server=None;self.host=self.cfg.get("server_ip","")
        if self.cfg.get("mode")=="principal":
            try:self.server=Server(self.receive);self.server.start();self.host="127.0.0.1"
            except Exception as e:messagebox.showerror("Vitakora","No se pudo iniciar el puesto principal:\n"+str(e))
        else:
            threading.Thread(target=self.auto,daemon=True).start()
        self.ui();self.load_history()
    def first(self):
        mode=messagebox.askyesno("Vitakora","¿Este equipo será el PUESTO PRINCIPAL?\n\nSí = Principal\nNo = Puesto normal")
        name=simpledialog.askstring("Vitakora","Nombre de farmacia:",initialvalue="Mi farmacia") or "Mi farmacia"
        ip="" if mode else (simpledialog.askstring("Vitakora","IP del puesto principal (vacío = detectar automáticamente):") or "")
        self.cfg={"mode":"principal" if mode else "normal","pharmacy":name,"server_ip":ip,"user":socket.gethostname()};savecfg(self.cfg)
    def ui(self):
        st=ttk.Style();st.configure("Nav.TFrame",background="#f4f5f3");st.configure("Title.TLabel",font=("Segoe UI",18,"bold"))
        top=ttk.Frame(self,padding=18);top.pack(fill="x");ttk.Label(top,text=self.cfg.get("pharmacy","Vitakora"),style="Title.TLabel").pack(side="left")
        self.status=ttk.Label(top,text="Conectando...");self.status.pack(side="right")
        body=ttk.Frame(self);body.pack(fill="both",expand=True)
        nav=ttk.Frame(body,padding=16,style="Nav.TFrame");nav.pack(side="left",fill="y")
        for x in ["Chats","Canales","Programados","Plantillas","Adjuntos","Usuarios","Equipos","Registro"]:ttk.Label(nav,text=x,padding=(8,10)).pack(anchor="w")
        ttk.Button(nav,text="Reconfigurar equipo",command=self.reconfigure).pack(anchor="w",fill="x",pady=(18,0))
        main=ttk.Frame(body,padding=20);main.pack(side="left",fill="both",expand=True)
        ttk.Label(main,text="Atención al cliente",font=("Segoe UI",16,"bold")).pack(anchor="w")
        self.chat=tk.Text(main,wrap="word",state="disabled",font=("Segoe UI",11),relief="flat");self.chat.pack(fill="both",expand=True,pady=15)
        row=ttk.Frame(main);row.pack(fill="x");self.entry=ttk.Entry(row,font=("Segoe UI",11));self.entry.pack(side="left",fill="x",expand=True,padx=(0,10));self.entry.bind("<Return>",lambda e:self.go())
        ttk.Button(row,text="Enviar",command=self.go).pack(side="right")
        self.after(300,self.update_status)
    def reconfigure(self):
        if messagebox.askyesno("Vitakora","¿Reconfigurar este equipo? La aplicación se cerrará para aplicar el cambio."):
            try:
                if CFG.exists(): CFG.unlink()
            except Exception:
                pass
            self.destroy()
    def probe(self):
        if not self.host:
            return False
        try:
            c=socket.create_connection((self.host,TCP),0.5)
            c.close()
            return True
        except OSError:
            return False
    def auto(self):
        import time
        while True:
            try:
                if not self.winfo_exists(): return
                if not self.probe():
                    found=discover(2)
                    if found:
                        self.host=found
                        self.cfg["server_ip"]=found
                        savecfg(self.cfg)
                time.sleep(2)
            except Exception:
                time.sleep(2)
    def update_status(self):
        if self.cfg.get("mode")=="principal":
            label="● Principal activo"
        elif self.probe():
            label="● Conectado a "+self.host
        elif self.host:
            label="○ Sin conexión ("+self.host+")"
        else:
            label="○ Buscando puesto principal"
        self.status.config(text=label)
        self.after(2000,self.update_status)
    def load_history(self):
        with db() as d:rows=d.execute("select sender,body,created from messages order by created limit 200").fetchall()
        for a,b,c in rows:self.add(a,b)
    def add(self,a,b):
        self.chat.config(state="normal");self.chat.insert("end",f"{a}\n{b}\n\n");self.chat.see("end");self.chat.config(state="disabled")
    def receive(self,a,b):self.after(0,lambda:self.add(a,b))
    def go(self):
        body=self.entry.get().strip()
        if not body:return
        if not self.host:messagebox.showwarning("Vitakora","No se ha encontrado el puesto principal.");return
        try:
            r=send(self.host,self.cfg.get("user","Puesto"),body)
            self.entry.delete(0,"end")
            if self.cfg.get("mode")!="principal":self.add(self.cfg.get("user","Yo"),body)
        except Exception as e:messagebox.showerror("Vitakora","No se pudo enviar el mensaje:\n"+str(e))
if __name__=="__main__":App().mainloop()
