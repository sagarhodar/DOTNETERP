import os

def combine_project_files(project_path, output_filename="combined_code.txt"):
    # File extensions we want to include
    include_extensions = ('.cs', '.xaml', '.csproj')
    #to add extra                                   '.iss','py','.txt','.md','.json','.xml','.config','.shader','.h','.cpp','.java','.js','.ts','.html','.css'
    # Directories we want to skip
    exclude_dirs = {'bin', 'obj', '.vs', '.git', 'Assets', 'LatoFont','AI','Installer'}

    output_path = os.path.join(project_path, output_filename)
    with open(output_path, 'w', encoding='utf-8') as outfile:
        outfile.write(f"PROJECT CONSOLIDATION: {os.path.basename(project_path)}\n")
        outfile.write("=" * 60 + "\n\n")

        for root, dirs, files in os.walk(project_path):
            # Modify dirs in-place to skip excluded directories
            dirs[:] = [d for d in dirs if d not in exclude_dirs]

            for file in files:
                if file.endswith(include_extensions):
                    file_path = os.path.join(root, file)
                    relative_path = os.path.relpath(file_path, project_path)
                    
                    outfile.write(f"\n{'#' * 80}\n")
                    outfile.write(f"### FILE: {relative_path}\n")
                    outfile.write(f"{'#' * 80}\n\n")
                    
                    try:
                        with open(file_path, 'r', encoding='utf-8') as infile:
                            content = infile.read()
                            # Use markdown-style code blocks to help the AI
                            ext = file.split('.')[-1]
                            outfile.write(f"```{ext}\n")
                            outfile.write(content)
                            outfile.write(f"\n```\n")
                    except Exception as e:
                        outfile.write(f"ERROR READING FILE: {str(e)}\n")
                    
                    outfile.write("\n")

    print(f"Done! All code consolidated into: {output_filename}")

if __name__ == "__main__":
    # Get the directory where the script is running
    current_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Run the combiner
    combine_project_files(current_dir)