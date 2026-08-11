// Resolves preview-capable executable projects on disk (plan.md 4.5.1/4.7): the project
// nearest to the active document first, otherwise executable projects under the solution.
package com.aprillz.mewui.preview

import java.io.File

object ProjectLocator {
    private val outputTypePattern = Regex("<OutputType>\\s*(WinExe|Exe)\\s*</OutputType>", RegexOption.IGNORE_CASE)

    fun findNearestProject(filePath: String, rootDirectory: String?): String? {
        var directory: File? = File(filePath).parentFile
        val root = rootDirectory?.let { File(it).absoluteFile }
        while (directory != null) {
            val project = directory.listFiles { file -> file.extension.equals("csproj", ignoreCase = true) }
                ?.minByOrNull { it.name }
            if (project != null) {
                return project.absolutePath
            }
            if (root != null && directory.absoluteFile == root) {
                return null
            }
            directory = directory.parentFile
        }
        return null
    }

    fun findExecutableProjects(rootDirectory: String?): List<String> {
        val root = rootDirectory?.let(::File) ?: return emptyList()
        if (!root.isDirectory) {
            return emptyList()
        }
        return root.walkTopDown()
            .onEnter { it.name != "bin" && it.name != "obj" && it.name != "node_modules" && it.name != ".git" }
            .filter { it.isFile && it.extension.equals("csproj", ignoreCase = true) }
            .filter { isExecutableProject(it.absolutePath) }
            .map { it.absolutePath }
            .sorted()
            .toList()
    }

    fun isExecutableProject(projectPath: String): Boolean =
        runCatching { outputTypePattern.containsMatchIn(File(projectPath).readText()) }.getOrDefault(false)
}
