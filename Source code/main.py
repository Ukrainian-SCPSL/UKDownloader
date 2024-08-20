import os
import platform
import requests
import tkinter as tk
from tkinter import filedialog
from tkinter import messagebox
from tkinter.ttk import Progressbar
import zipfile
import threading

def check_internet_connection():
    try:
        requests.get("http://www.google.com", timeout=5)
        return True
    except requests.ConnectionError:
        return False

def download_files():
    if not check_internet_connection():
        messagebox.showerror("Помилка!", "Будь ласка, підключіть інтернет, щоб завантажити локалізацію.\nЯкщо у вас є інтернет, але не вдається. Внесіть цей .exe файл до білого списку антивіруса.")
        return

    if platform.system() not in ['Windows', 'Windows']:
        messagebox.showerror("Помилка!", "На жаль, програма підтримує встановлення тільки на Windows 10 або Windows 11.")
        return

    repo_url = 'https://api.github.com/repos/Ukrainian-SCPSL/Ukrainian-language/releases/latest'
    response = requests.get(repo_url)

    if response.status_code == 200:
        release_info = response.json()
        assets = release_info['assets']

        download_path = filedialog.askdirectory(initialdir=os.path.join(os.path.expanduser("~"), "Documents"), title="Виберіть папку Translations у папці гри.")

        if download_path:
            cash_path = os.path.join(os.path.expanduser("~"), "Documents", "UKDownloader_cash")
            os.makedirs(cash_path, exist_ok=True)

            progress_bar["value"] = 0
            progress_bar["maximum"] = len(assets)
            progress_window.deiconify()
            download_thread = threading.Thread(target=start_downloads, args=(assets, cash_path, download_path))
            download_thread.start()
        else:
            messagebox.showwarning("Увага!", "Виберіть папку Translations у папці гри.")
    else:
        messagebox.showerror("Помилка!", "Помилка при отриманні інформації")

def start_downloads(assets, cash_path, download_path):
    zip_assets = [asset for asset in assets if asset['name'].endswith('.zip') and not asset['name'].endswith('Source.zip')]
    if not zip_assets:
        messagebox.showerror("Помилка!", "Не знайдено ZIP-архів для завантаження.")
        return

    for index, asset in enumerate(zip_assets):
        download_url = asset['browser_download_url']
        downloaded_file_path = download_file(download_url, cash_path)

        if not is_zip_file(downloaded_file_path):
            messagebox.showerror("Помилка!", f"Файл {downloaded_file_path} не є ZIP-архівом або він пошкоджений.")
            return
        
        progress_bar["value"] = index + 1

    # Unzip downloaded files to the chosen path
    unzip_files(cash_path, download_path)

    # Remove all files except ZIP archives from UKDownloader_cash
    remove_non_zip_files(cash_path)

    messagebox.showinfo("Успішно!", "Локалізацією успішно завантажено!\nМожете закрити програму.")
    progress_window.withdraw()

def download_file(url, path):
    filename = url.split('/')[-1]
    file_path = os.path.join(path, filename)
    response = requests.get(url, stream=True)

    with open(file_path, 'wb') as f:
        total_length = int(response.headers.get('content-length'))
        downloaded = 0
        for chunk in response.iter_content(chunk_size=1024):
            if chunk:
                f.write(chunk)
                downloaded += len(chunk)
                progress = int(downloaded / total_length * 100)
                progress_text.set(f"Завантажено: {progress}%")
    response.close()
    return file_path

def is_zip_file(file_path):
    """Check if the file is a valid ZIP archive."""
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
    tutorial_text = "Щоб встановити локалізацію, вам потрібно пройтися за цими пунктами:\n1. Відкрити Steam -> Бібліотека -> Керування -> Подивитися локальні файли\n2. Відкрити папку Translation\n3. Копіювати зверху шлях до папки\n4. У програмі натиснути «Завантажити»\n5. Вставити в рядок вище шлях до папки Translation і натиснути «Вибрати папку».\n6. Очікувати скачування.\n\nГотово! Ви можете в налаштуваннях гри поставити українську мову.\nПрограму ви можете собі залишити, щоб у наступні рази оновлювати локалізацію.\n\nТакож у папці Документи/UKDownloader_cash зберігається кеш інсталяції, можете після встановлення локалізації очищати цю папку."
    messagebox.showinfo("Tutorial", tutorial_text)

# GUI setup
root = tk.Tk()
root.title("UK Downloader")

# Calculate the center of the screen
screen_width = root.winfo_screenwidth()
screen_height = root.winfo_screenheight()
window_width = 500
window_height = 200
x_position = (screen_width // 2) - (window_width // 2)
y_position = (screen_height // 2) - (window_height // 2)
root.geometry(f"{window_width}x{window_height}+{x_position}+{y_position}")

# Set custom icon
if platform.system() == "Windows":
    root.iconbitmap(default='ico.ico')

# Set minimum window size
root.minsize(window_width, window_height)

info_label = tk.Label(root, text="Українська локалізація для SCP:SL", font=("Arial", 20))
info_label.pack(pady=0)

info_label2 = tk.Label(root, text="Локалізація створена Narin'ом і Fencer'ом.", font=("Arial", 10))
info_label2.pack(pady=0)

version_label = tk.Label(root, text="Версія: 1.0.2\nПрограма тільки підтримує ОС: Windows 10 та Windows 11.\nПрограму створено Narin'ом.", font=("Arial", 8), anchor="se", wraplength=480)
version_label.pack(side="bottom", padx=10, pady=10)

download_button = tk.Button(root, text="Завантажити", command=download_files)
download_button.pack(pady=5)

tutorial_button = tk.Button(root, text="Tutorial", command=open_tutorial)
tutorial_button.pack(pady=3)

# Progress bar window
progress_window = tk.Toplevel(root)
progress_window.title("Прогрес завантаження")

# Calculate the position of the progress window
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

root.mainloop()
