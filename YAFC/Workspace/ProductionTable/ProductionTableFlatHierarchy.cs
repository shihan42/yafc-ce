﻿using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    /// <summary>
    /// This is a flat hierarchy that can be used to display a table with nested groups in a single list.
    /// The method <see cref="BuildFlatHierarchy"/> flattens the tree.
    /// </summary>
    public class FlatHierarchy<TRow, TGroup> where TRow : ModelObject<TGroup>, IGroupedElement<TGroup> where TGroup : ModelObject<ModelObject>, IElementGroup<TRow> {
        private readonly DataGrid<TRow> grid;
        private readonly List<TRow> flatRecipes = new List<TRow>();
        private readonly List<TGroup> flatGroups = new List<TGroup>();
        private readonly List<bool> isGreyedOut = new List<bool>();
        private TRow draggingRecipe;
        private TGroup root;
        private bool rebuildRequired;
        private readonly Action<ImGui, TGroup> drawTableHeader;
        private readonly string emptyGroupMessage;
        private readonly bool buildExpandedGroupRows;

        public FlatHierarchy(DataGrid<TRow> grid, Action<ImGui, TGroup> drawTableHeader, string emptyGroupMessage = "This is an empty group", bool buildExpandedGroupRows = true) {
            this.grid = grid;
            this.drawTableHeader = drawTableHeader;
            this.emptyGroupMessage = emptyGroupMessage;
            this.buildExpandedGroupRows = buildExpandedGroupRows;
        }

        public float width => grid.width;
        public void SetData(TGroup table) {
            root = table;
            rebuildRequired = true;
        }

        private (TGroup, int) FindDragginRecipeParentAndIndex() {
            int index = flatRecipes.IndexOf(draggingRecipe);
            if (index == -1) {
                return default;
            }

            int currentIndex = 0;
            for (int i = index - 1; i >= 0; i--) {
                if (flatRecipes[i] is TRow recipe) {
                    var group = recipe.subgroup;
                    if (group != null && group.expanded) {
                        return (group, currentIndex);
                    }
                }
                else {
                    i = flatRecipes.LastIndexOf(flatGroups[i].owner as TRow, i);
                }

                currentIndex++;
            }
            return (root, currentIndex);
        }

        private void ActuallyMoveDraggingRecipe() {
            var (parent, index) = FindDragginRecipeParentAndIndex();
            if (parent == null) {
                return;
            }

            if (draggingRecipe.owner == parent && parent.elements[index] == draggingRecipe) {
                return;
            }

            _ = draggingRecipe.owner.RecordUndo().elements.Remove(draggingRecipe);
            draggingRecipe.SetOwner(parent);
            parent.RecordUndo().elements.Insert(index, draggingRecipe);
        }

        private void MoveFlatHierarchy(TRow from, TRow to) {
            draggingRecipe = from;
            int indexFrom = flatRecipes.IndexOf(from);
            int indexTo = flatRecipes.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        private void MoveFlatHierarchy(TRow from, TGroup to) {
            draggingRecipe = from;
            int indexFrom = flatRecipes.IndexOf(from);
            int indexTo = flatGroups.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        private readonly Stack<float> depthStart = new Stack<float>();

        private void SwapBgColor(ref SchemeColor color) {
            color = nextRowIsGreyedOut ? SchemeColor.GreyedOutBackground :
                color == SchemeColor.Background ? SchemeColor.PureBackground : SchemeColor.Background;
        }

        public bool nextRowIsGreyedOut { get; private set; }

        public void Build(ImGui gui) {
            if (draggingRecipe != null && !gui.isDragging) {
                ActuallyMoveDraggingRecipe();
                draggingRecipe = null;
                rebuildRequired = true;
            }
            if (rebuildRequired) {
                Rebuild();
            }

            grid.BeginBuildingContent(gui);
            var bgColor = SchemeColor.PureBackground;
            int depth = 0;
            float depWidth = 0f;
            for (int i = 0; i < flatRecipes.Count; i++) {
                var recipe = flatRecipes[i];
                var item = flatGroups[i];

                nextRowIsGreyedOut = typeof(TRow) == typeof(RecipeRow) && isGreyedOut[i];

                if (recipe != null) {
                    if (!recipe.visible) {
                        if (item != null) {
                            i = flatGroups.LastIndexOf(item);
                        }

                        continue;
                    }

                    if (item != null) {
                        depth++;
                        SwapBgColor(ref bgColor);
                        depWidth = depth * 0.5f;
                        if (gui.isBuilding) {
                            depthStart.Push(gui.statePosition.Bottom);
                        }
                    }

                    if (buildExpandedGroupRows || item == null) {
                        var rect = grid.BuildRow(gui, recipe, depWidth);
                        if (item == null && gui.InitiateDrag(rect, rect, recipe, bgColor)) {
                            draggingRecipe = recipe;
                        }
                        else if (gui.ConsumeDrag(rect.Center, recipe)) {
                            MoveFlatHierarchy(gui.GetDraggingObject<TRow>(), recipe);
                        }

                        if (nextRowIsGreyedOut) {
                            gui.DrawRectangle(rect, SchemeColor.GreyedOutBackground);
                        }
                    }
                    if (item != null) {
                        if (item.elements.Count == 0) {
                            using (gui.EnterGroup(new Padding(0.5f + depWidth, 0.5f, 0.5f, 0.5f))) {
                                gui.BuildText(emptyGroupMessage, wrap: true); // set color if nested row is empty
                            }
                        }

                        if (drawTableHeader != null) {
                            using (gui.EnterGroup(new Padding(0.5f + depWidth, 0.5f, 0.5f, 0.5f))) {
                                drawTableHeader(gui, item);
                            }
                        }
                    }
                }
                else {
                    if (gui.isBuilding) {
                        float top = depthStart.Pop();
                        // set color bgColor if row is nested table and not collapsed
                        gui.DrawRectangle(new Rect(depWidth, top, grid.width - depWidth, gui.statePosition.Bottom - top), bgColor, RectangleBorder.Thin);
                    }
                    SwapBgColor(ref bgColor);
                    depth--;
                    depWidth = depth * 0.5f;
                    _ = gui.AllocateRect(20f, 0.5f);
                }

                nextRowIsGreyedOut = false;
            }
            var fullRect = grid.EndBuildingContent(gui);
            gui.DrawRectangle(fullRect, SchemeColor.PureBackground); // whole background color
        }

        private void Rebuild() {
            flatRecipes.Clear();
            flatGroups.Clear();
            isGreyedOut.Clear();
            BuildFlatHierarchy(root);
            rebuildRequired = false;
        }

        private int greyOutStack = 0;

        private void BuildFlatHierarchy(TGroup table) {
            foreach (var recipe in table.elements) {
                flatRecipes.Add(recipe);

                bool greyOut = false;
                if (recipe is RecipeRow r && (r.tag == 1 || greyOutStack > 0)) {
                    greyOutStack++;
                    greyOut = true;
                    isGreyedOut.Add(true);
                }
                else {
                    isGreyedOut.Add(false);
                }

                var sub = recipe.subgroup;
                if (sub != null && sub.expanded) {
                    flatGroups.Add(sub);
                    BuildFlatHierarchy(sub);
                    flatRecipes.Add(null);
                    flatGroups.Add(sub);
                    isGreyedOut.Add(false);
                }
                else {
                    flatGroups.Add(null);
                }

                if (greyOut) {
                    greyOutStack--;
                }
            }
        }

        public void BuildHeader(ImGui gui) {
            grid.BuildHeader(gui);
        }
    }
}
