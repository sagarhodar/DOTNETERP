import os

# List of files/folders to ignore
ignore_list = [
    "node_modules",
    ".vs",
    "dist",
    ".git",
    ".env.local",
    "package-lock.json",
    "__pycache__",
    ".DS_Store",
    "*.log",
    "obj",
    "bin",
    "*.pyc",
    ".env",
    "raw_files.zip",
    "venv",
    "AI",
    "Installer",
    "code.py",
    "Ojaswat.iss",
    "Update_Ojaswat.iss",
    "combined_code.txt",
    "copy.py",
    "Project Folder Structure Tree.py",
    "Project Folder Structure.txt",
    "Ojaswat_installer.ps1",
    "Update_installer.ps1"
]

def should_ignore(name):
    for pattern in ignore_list:
        if pattern.startswith("*") and name.endswith(pattern[1:]):
            return True
        if name == pattern:
            return True
    return False


def print_directory_tree(root_dir, file, prefix=""):
    entries = sorted(os.listdir(root_dir))
    entries = [e for e in entries if not should_ignore(e)]
    entries_count = len(entries)

    for index, entry in enumerate(entries):
        path = os.path.join(root_dir, entry)
        is_last = index == (entries_count - 1)
        connector = "└── " if is_last else "├── "

        if os.path.isdir(path):
            line = f"{prefix}{connector}{entry}/"
            file.write(line + "\n")

            new_prefix = prefix + ("    " if is_last else "│   ")
            print_directory_tree(path, file, new_prefix)
        else:
            line = f"{prefix}{connector}{entry}"
            file.write(line + "\n")


if __name__ == "__main__":

    # Folder where THIS python file exists
    script_dir = os.path.dirname(os.path.abspath(__file__))

    output_file = os.path.join(script_dir, "Project Folder Structure.txt")

    with open(output_file, "w", encoding="utf-8") as f:
        f.write(script_dir + "\n")
        print_directory_tree(script_dir, f)

    print(f"Folder structure saved to:\n{output_file}")