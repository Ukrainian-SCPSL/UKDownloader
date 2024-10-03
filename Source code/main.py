import os
import platform
import requests
import tkinter as tk
from tkinter import filedialog, messagebox
from tkinter.ttk import Progressbar
import zipfile
import threading
import json
import sys
import subprocess
import webbrowser

PROGRAMVERSION = 'v1.0.2'

def check_internet_connection():
    try:
        requests.get("https://github.com/Ukrainian-SCPSL/UKDownloader", timeout=5)
        return True
    except requests.ConnectionError:
        return False

def check_for_updates():
    update_label.config(text="Перевірка на наявність оновлення...")
    try:
        response = requests.get('https://api.github.com/repos/Ukrainian-SCPSL/UKDownloader/releases/latest', timeout=10)
        if response.status_code == 200:
            release_info = response.json()
            latest_version = release_info['tag_name']
            if latest_version == PROGRAMVERSION:
                update_window.destroy()
                root.deiconify()
                get_localization_version()
            else:
                update_label.config(text="Завантаження оновлення...")
                cache_path = os.path.join(os.path.expanduser("~"), "Documents", "UKDownloader_cash")
                os.makedirs(cache_path, exist_ok=True)
                assets = release_info.get('assets', [])
                setup_asset = None
                for asset in assets:
                    if asset['name'] == 'UKDownloader.Setup.exe':
                        setup_asset = asset
                        break
                if setup_asset:
                    download_url = setup_asset['browser_download_url']
                    setup_file_path = os.path.join(cache_path, 'UKDownloader.Setup.exe')
                    response = requests.get(download_url, stream=True)
                    with open(setup_file_path, 'wb') as f:
                        for chunk in response.iter_content(chunk_size=1024):
                            if chunk:
                                f.write(chunk)
                    response.close()
                    subprocess.Popen([setup_file_path], shell=True)
                    sys.exit()
                else:
                    messagebox.showerror("Помилка!", "Не вдалося знайти інсталятор оновлення.")
                    sys.exit()
        else:
            messagebox.showerror("Помилка!", "Не вдалося отримати інформацію про оновлення.")
            sys.exit()
    except requests.RequestException:
        messagebox.showerror("Помилка!", "Не вдалося підключитися до серверів GitHub.")
        sys.exit()

def get_localization_version():
    try:
        response = requests.get('https://api.github.com/repos/Ukrainian-SCPSL/Ukrainian-language/releases/latest', timeout=10)
        if response.status_code == 200:
            release_info = response.json()
            localization_version = release_info['tag_name']
            config_data = {}
            if os.path.exists('config.json'):
                with open('config.json', 'r', encoding='utf-8') as config_file:
                    config_data = json.load(config_file)
            last_localization_version = config_data.get('lastlocalization_version', '-')
            if last_localization_version == '-':
                combined_info_label.config(text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: -\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.")
            elif last_localization_version == localization_version:
                combined_info_label.config(text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: {localization_version}\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.")
            else:
                combined_info_label.config(text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: {last_localization_version} (застаріла версія)\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.")
        else:
            combined_info_label.config(text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: -\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.")
    except requests.RequestException:
        combined_info_label.config(text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: -\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.")

def select_folder():
    folder_selected = filedialog.askdirectory(initialdir=os.path.join(os.path.expanduser("~"), "Documents"), title="Оберіть папку Translations у папці гри.")
    if folder_selected:
        if os.path.exists('config.json'):
            with open('config.json', 'r', encoding='utf-8') as config_file:
                config_data = json.load(config_file)
        else:
            config_data = {}
        config_data['localization_path'] = folder_selected
        with open('config.json', 'w', encoding='utf-8') as config_file:
            json.dump(config_data, config_file)
        localization_path.set(folder_selected)

def download_files():
    if not localization_path.get() or localization_path.get() == "Оберіть папку Translations у папці гри.":
        messagebox.showwarning("Увага!", "Оберіть папку Translations у папці гри.")
        return

    if not check_internet_connection():
        messagebox.showerror("Помилка!", "Будь ласка, підключіть інтернет, щоб завантажити локалізацію.\nЯкщо у вас є інтернет, але не вдається. Внесіть цей .exe файл до білого списку антивіруса.")
        return

    if platform.system() != 'Windows':
        messagebox.showerror("Помилка!", "На жаль, програма підтримує встановлення тільки на Windows 10 або Windows 11.")
        return

    repo_url = 'https://api.github.com/repos/Ukrainian-SCPSL/Ukrainian-language/releases/latest'
    response = requests.get(repo_url)

    if response.status_code == 200:
        release_info = response.json()
        assets = release_info['assets']
        localization_version = release_info['tag_name']

        download_path = localization_path.get()

        if download_path:
            cache_path = os.path.join(os.path.expanduser("~"), "Documents", "UKDownloader_cash")
            os.makedirs(cache_path, exist_ok=True)

            progress_bar["value"] = 0
            progress_bar["maximum"] = len(assets)
            progress_window.deiconify()
            download_thread = threading.Thread(target=start_downloads, args=(assets, cache_path, download_path, localization_version))
            download_thread.start()
        else:
            messagebox.showwarning("Увага!", "Оберіть папку Translations у папці гри.")
    else:
        messagebox.showerror("Помилка!", "Помилка при отриманні інформації")

def start_downloads(assets, cache_path, download_path, localization_version):
    zip_assets = [asset for asset in assets if asset['name'].endswith('.zip') and not asset['name'].endswith('Source.zip')]
    if not zip_assets:
        messagebox.showerror("Помилка!", "Не знайдено ZIP-архів для завантаження.")
        return

    for index, asset in enumerate(zip_assets):
        download_url = asset['browser_download_url']
        downloaded_file_path = download_file(download_url, cache_path)

        if not is_zip_file(downloaded_file_path):
            messagebox.showerror("Помилка!", f"Файл {downloaded_file_path} не є ZIP-архівом або він пошкоджений.")
            return

        progress_bar["value"] = index + 1

    unzip_files(cache_path, download_path)

    remove_non_zip_files(cache_path)

    if os.path.exists('config.json'):
        with open('config.json', 'r', encoding='utf-8') as config_file:
            config_data = json.load(config_file)
    else:
        config_data = {}
    config_data['lastlocalization_version'] = localization_version
    with open('config.json', 'w', encoding='utf-8') as config_file:
        json.dump(config_data, config_file)

    combined_info_label.config(text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: {localization_version}\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.")

    messagebox.showinfo("Успішно!", "Локалізацію успішно завантажено!\nМожете закрити програму.")
    progress_window.withdraw()

def download_file(url, path):
    filename = url.split('/')[-1]
    file_path = os.path.join(path, filename)
    response = requests.get(url, stream=True)

    with open(file_path, 'wb') as f:
        total_length = int(response.headers.get('content-length', 0))
        downloaded = 0
        for chunk in response.iter_content(chunk_size=1024):
            if chunk:
                f.write(chunk)
                downloaded += len(chunk)
                if total_length > 0:
                    progress = int(downloaded / total_length * 100)
                    progress_text.set(f"Завантажено: {progress}%")
    response.close()
    return file_path

def is_zip_file(file_path):
    try:
        with zipfile.ZipFile(file_path, 'r') as zip_ref:
            return True
    except zipfile.BadZipFile:
        return False

def unzip_files(zip_path, extract_path):
    zip_files = [f for f in os.listdir(zip_path) if f.endswith('.zip')]
    for zip_file in zip_files:
        with zipfile.ZipFile(os.path.join(zip_path, zip_file), 'r') as zip_ref:
            zip_ref.extractall(extract_path)

def remove_non_zip_files(folder_path):
    files = os.listdir(folder_path)
    for file in files:
        file_path = os.path.join(folder_path, file)
        if not file.endswith('.zip'):
            os.remove(file_path)

def open_tutorial():
    tutorial_text = "Щоб встановити локалізацію, вам потрібно пройтися за цими пунктами:\n1. Відкрити Steam -> Бібліотека -> Керування -> Подивитися локальні файли\n2. Відкрийте папку Translations.\n3. Скопіюйте зверху шлях до папки.\n4. У програмі натисніть на  «...».\n5. Вставте в рядок вище шлях до папки Translations і натисніть на «Вибрати папку».\n6. Натисніть на «Завантажити».\n7. Очікувайте на завантаження.\n\nГотово! Ви можете в налаштуваннях гри поставити українську мову.\n\nПрограму ви можете собі залишити, щоб у наступні рази оновлювати локалізацію.\n\nТакож у папці Документи/UKDownloader_cash зберігається кеш інсталяції, можете після встановлення локалізації очищати цю папку."
    messagebox.showinfo("Tutorial", tutorial_text)

def open_discord():
    webbrowser.open("https://discord.gg/c67Md8nW5u")

root = tk.Tk()
root.withdraw()

update_window = tk.Toplevel(root)
update_window.title("Перевірка оновлень")

screen_width = root.winfo_screenwidth()
screen_height = root.winfo_screenheight()
update_window_width = 300
update_window_height = 100
x_position = (screen_width // 2) - (update_window_width // 2)
y_position = (screen_height // 2) - (update_window_height // 2)
update_window.geometry(f"{update_window_width}x{update_window_height}+{x_position}+{y_position}")

update_label = tk.Label(update_window, text="Перевірка на наявність оновлення...", font=("Arial", 12))
update_label.pack(pady=20)

update_window.after(100, check_for_updates)

root.title("UK Downloader")
window_width = 500
window_height = 250
x_position = (screen_width // 2) - (window_width // 2)
y_position = (screen_height // 2) - (window_height // 2)
root.geometry(f"{window_width}x{window_height}+{x_position}+{y_position}")
root.resizable(False, False)

if platform.system() == "Windows":
    root.iconbitmap(default='ico.ico')

info_label = tk.Label(root, text="Українська локалізація для SCP:SL", font=("Arial", 20))
info_label.pack(pady=0)

info_label2 = tk.Label(root, text="Локалізація створена Narin'ом і Fencer'ом.", font=("Arial", 10))
info_label2.pack(pady=0)

localization_path = tk.StringVar()
localization_path.set("Оберіть папку Translations у папці гри.")

path_frame = tk.Frame(root)
path_frame.pack(pady=5)

path_entry = tk.Entry(path_frame, textvariable=localization_path, state='readonly', width=50)
path_entry.pack(side='left', padx=5)

browse_button = tk.Button(path_frame, text="...", command=select_folder)
browse_button.pack(side='left')

download_button = tk.Button(root, text="Завантажити", command=download_files)
download_button.pack(pady=5)

tutorial_button = tk.Button(root, text="Tutorial", command=open_tutorial)
tutorial_button.pack(pady=(3, 10))

combined_info_label = tk.Label(root, text=f"Версія програми: {PROGRAMVERSION}\nВерсія локалізації: -\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.", font=("Arial", 8), anchor="se", wraplength=480)
combined_info_label.pack(pady=0)

discord_image = tk.PhotoImage(file="discordlogo.png").subsample(20, 20)
discord_button = tk.Button(root, image=discord_image, command=open_discord, borderwidth=0)
discord_button.place(relx=1.0, rely=1.0, anchor='se', x=-10, y=-10)

progress_window = tk.Toplevel(root)
progress_window.title("Прогрес завантаження")

progress_window_width = 300
progress_window_height = 100
progress_x_position = (screen_width // 2) - (progress_window_width // 2)
progress_y_position = (screen_height // 2) - (progress_window_height // 2)
progress_window.geometry(f"{progress_window_width}x{progress_window_height}+{progress_x_position}+{progress_y_position}")
progress_window.withdraw()

progress_text = tk.StringVar()
progress_label = tk.Label(progress_window, textvariable=progress_text)
progress_label.pack(pady=10)

progress_bar = Progressbar(progress_window, length=250, mode='determinate')
progress_bar.pack(pady=5)

if os.path.exists('config.json'):
    with open('config.json', 'r', encoding='utf-8') as config_file:
        config_data = json.load(config_file)
        localization_path.set(config_data.get('localization_path', "Оберіть папку Translations у папці гри."))
else:
    config_data = {}

root.mainloop()
