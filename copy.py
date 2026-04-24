import os
import shutil
import re

def get_next_version(parent_dir, project_name):
    
    version_pattern = re.compile(rf"{project_name}_V(\d+)")
    max_version = 1

    for folder in os.listdir(parent_dir):
        match = version_pattern.match(folder)
        if match:
            version = int(match.group(1))
            max_version = max(max_version, version)

    return max_version + 1


def copy_project(src_dir):

    project_name = os.path.basename(src_dir)
    parent_dir = os.path.dirname(src_dir)

    next_version = get_next_version(parent_dir, project_name)

    new_folder_name = f"{project_name}_V{next_version:02d}"
    new_path = os.path.join(parent_dir, new_folder_name)

    print(f"Creating new version: {new_folder_name}")

    shutil.copytree(
        src_dir,
        new_path,
        ignore=shutil.ignore_patterns(
            'bin',
            'obj',
            '.vs',
            '.git',
            'CODE_BAC'
        )
    )

    print(f"Project duplicated at:\n{new_path}")


if __name__ == "__main__":

    current_dir = os.path.dirname(os.path.abspath(__file__))

    copy_project(current_dir)